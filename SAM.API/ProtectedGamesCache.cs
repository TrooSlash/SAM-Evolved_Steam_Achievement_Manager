using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace SAM.API
{
    /// <summary>
    /// Production-quality file-based cache tracking which games have protected (server-validated) achievements.
    /// Shared between SAM.Picker (reads + background scan) and SAM.Game (writes).
    ///
    /// Architecture:
    /// - Cache file: lib/protected_cache.txt — one "appId|timestamp" per line.
    /// - Cross-process synchronization: named Mutex "Local\SAM_ProtectedCache".
    /// - Atomic writes: write to .tmp then File.Move (replace).
    /// - Cache invalidation: entries older than MaxCacheAgeDays are re-scanned.
    /// - Binary KV parser: hardened recursive scanner with EOF checks, depth limit, read validation.
    /// </summary>
    public static class ProtectedGamesCache
    {
        private const string MutexName = @"Local\SAM_ProtectedCache";
        private const int MutexTimeoutMs = 3000;
        private const int MaxRecursionDepth = 64;
        private const int MaxCacheAgeDays = 7;

        // Valve binary KV type codes
        private const byte KvTypeNone = 0;      // Subtree (children follow, terminated by KvTypeEnd)
        private const byte KvTypeString = 1;     // Null-terminated UTF-8 string value
        private const byte KvTypeInt32 = 2;      // 4-byte signed integer
        private const byte KvTypeFloat32 = 3;    // 4-byte IEEE 754 float
        private const byte KvTypePointer = 4;    // 4-byte pointer (treated as uint32)
        private const byte KvTypeWideString = 5; // Null-terminated UTF-16 string (rare, skip)
        private const byte KvTypeColor = 6;      // 4-byte RGBA color
        private const byte KvTypeUInt64 = 7;     // 8-byte unsigned integer
        private const byte KvTypeEnd = 8;        // End-of-subtree marker

        private struct CacheEntry
        {
            public uint AppId;
            public long TimestampUtc; // Unix seconds when entry was added
        }

        private static string GetCachePath()
        {
            string baseDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location
                ?? Assembly.GetExecutingAssembly().Location);
            return Path.Combine(baseDir, "lib", "protected_cache.txt");
        }

        /// <summary>
        /// Loads the cache, returning all appIds that are marked as protected.
        /// Thread-safe: uses named Mutex for cross-process synchronization.
        /// </summary>
        public static HashSet<uint> Load()
        {
            var result = new HashSet<uint>();
            try
            {
                string path = GetCachePath();
                if (!File.Exists(path)) return result;

                string[] lines;
                using (new MutexGuard())
                {
                    lines = File.ReadAllLines(path);
                }

                foreach (string line in lines)
                {
                    var entry = ParseCacheLine(line);
                    if (entry.AppId != 0)
                    {
                        result.Add(entry.AppId);
                    }
                }
            }
            catch
            {
                // Cache is best-effort — never crash the caller
            }
            return result;
        }

        /// <summary>
        /// Loads cache entries with timestamps for invalidation checks.
        /// </summary>
        private static Dictionary<uint, long> LoadWithTimestamps()
        {
            var result = new Dictionary<uint, long>();
            try
            {
                string path = GetCachePath();
                if (!File.Exists(path)) return result;

                foreach (string line in File.ReadAllLines(path))
                {
                    var entry = ParseCacheLine(line);
                    if (entry.AppId != 0 && !result.ContainsKey(entry.AppId))
                    {
                        result[entry.AppId] = entry.TimestampUtc;
                    }
                }
            }
            catch
            {
                // Best-effort
            }
            return result;
        }

        private static CacheEntry ParseCacheLine(string line)
        {
            var entry = new CacheEntry();
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) return entry;

            // Format: "appId" (legacy) or "appId|timestamp"
            int pipeIdx = trimmed.IndexOf('|');
            if (pipeIdx > 0)
            {
                if (uint.TryParse(trimmed.Substring(0, pipeIdx), out uint id))
                {
                    entry.AppId = id;
                    if (long.TryParse(trimmed.Substring(pipeIdx + 1), out long ts))
                        entry.TimestampUtc = ts;
                    else
                        entry.TimestampUtc = 0; // legacy entry, treat as expired for re-validation
                }
            }
            else
            {
                // Legacy format: just appId
                if (uint.TryParse(trimmed, out uint id))
                {
                    entry.AppId = id;
                    entry.TimestampUtc = 0;
                }
            }
            return entry;
        }

        /// <summary>
        /// Marks a game as having protected achievements.
        /// Cross-process safe: acquires Mutex, reads file, deduplicates, writes atomically.
        /// </summary>
        public static void MarkProtected(uint appId)
        {
            try
            {
                string path = GetCachePath();
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (new MutexGuard())
                {
                    var entries = LoadWithTimestamps();
                    entries[appId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    WriteCache(path, entries);
                }
            }
            catch
            {
                // Cache is best-effort
            }
        }

        /// <summary>
        /// Atomically writes the entire cache file.
        /// Writes to a .tmp file first, then replaces the original.
        /// This prevents corruption if the process is killed mid-write.
        /// </summary>
        private static void WriteCache(string path, Dictionary<uint, long> entries)
        {
            string tmpPath = path + ".tmp";
            var sb = new StringBuilder(entries.Count * 20);
            foreach (var kvp in entries)
            {
                sb.Append(kvp.Key.ToString(CultureInfo.InvariantCulture));
                sb.Append('|');
                sb.Append(kvp.Value.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine();
            }
            File.WriteAllText(tmpPath, sb.ToString(), Encoding.UTF8);

            // Atomic replace: .NET Framework doesn't have File.Move(overwrite),
            // so delete + move. The Mutex protects against races.
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmpPath, path);
        }

        /// <summary>
        /// Scans Steam's appcache/stats/ folder for UserGameStatsSchema_{appId}.bin files.
        /// Parses each schema to check if any achievement has (permission &amp; 3) != 0.
        /// Uses cache invalidation: entries older than MaxCacheAgeDays are re-scanned.
        /// Returns the set of appIds that have protected achievements.
        /// </summary>
        public static HashSet<uint> ScanSteamSchemas(IEnumerable<uint> appIds)
        {
            var result = new HashSet<uint>();
            Dictionary<uint, long> cached;

            try
            {
                using (new MutexGuard())
                {
                    cached = LoadWithTimestamps();
                }
            }
            catch
            {
                cached = new Dictionary<uint, long>();
            }

            long nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long maxAgeSeconds = MaxCacheAgeDays * 86400L;

            // Populate result with still-valid cached entries
            foreach (var kvp in cached)
            {
                bool isExpired = kvp.Value == 0 || (nowUtc - kvp.Value) > maxAgeSeconds;
                if (!isExpired)
                {
                    result.Add(kvp.Key);
                }
            }

            try
            {
                string steamPath = Steam.GetInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return result;

                string statsDir = Path.Combine(steamPath, "appcache", "stats");
                if (!Directory.Exists(statsDir)) return result;

                bool cacheChanged = false;

                foreach (uint appId in appIds)
                {
                    if (result.Contains(appId)) continue; // already known and fresh

                    string schemaFile = Path.Combine(statsDir,
                        string.Format(CultureInfo.InvariantCulture, "UserGameStatsSchema_{0}.bin", appId));
                    if (!File.Exists(schemaFile)) continue;

                    try
                    {
                        if (SchemaHasProtectedAchievements(schemaFile))
                        {
                            result.Add(appId);
                            cached[appId] = nowUtc;
                            cacheChanged = true;
                        }
                    }
                    catch
                    {
                        // Skip unparseable files — don't mark as protected
                    }
                }

                // Remove expired entries that were NOT re-confirmed by scan
                var toRemove = new List<uint>();
                foreach (var kvp in cached)
                {
                    bool isExpired = kvp.Value == 0 || (nowUtc - kvp.Value) > maxAgeSeconds;
                    if (isExpired && !result.Contains(kvp.Key))
                    {
                        toRemove.Add(kvp.Key);
                        cacheChanged = true;
                    }
                }
                foreach (uint id in toRemove)
                    cached.Remove(id);

                // Persist changes atomically
                if (cacheChanged)
                {
                    try
                    {
                        string path = GetCachePath();
                        string dir = Path.GetDirectoryName(path);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        using (new MutexGuard())
                        {
                            WriteCache(path, cached);
                        }
                    }
                    catch
                    {
                        // Best-effort persistence
                    }
                }
            }
            catch
            {
                // Best-effort scan
            }

            return result;
        }

        #region Binary KV Parser (hardened)

        /// <summary>
        /// Opens a schema .bin file and scans its binary KeyValue tree
        /// for achievement "permission" nodes with (value &amp; 3) != 0.
        /// Uses FileShare.ReadWrite to avoid conflicts with Steam client.
        ///
        /// IMPORTANT: Only checks permission under achievement nodes (inside "bits" subtrees),
        /// NOT stat-level permissions. Schema KV structure:
        ///   [AppId] → stats → [stat_id] → bits → [ach_id] → permission
        /// Stat-level permission ([stat_id] → permission) is ignored — it controls stat
        /// read/write access, not achievement server-validation.
        /// </summary>
        private static bool SchemaHasProtectedAchievements(string path)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return ScanKvTree(stream, 0, false);
            }
        }

        /// <summary>
        /// Recursively walks a Valve binary KV tree.
        /// Only checks "permission" Int32 values when <paramref name="insideBits"/> is true,
        /// meaning we are inside a "bits" subtree (achievement definitions).
        ///
        /// Binary KV format per node:
        ///   [1 byte: type] [null-terminated UTF-8 string: name] [value depends on type]
        ///   Type 0 (None): children follow, terminated by Type 8 (End)
        ///   Type 8 (End): no name, no value — marks end of current subtree
        ///
        /// The insideBits flag is set to true when entering a subtree named "bits".
        /// This ensures we only flag achievement-level permissions, not stat-level ones.
        /// </summary>
        private static bool ScanKvTree(Stream stream, int depth, bool insideBits)
        {
            if (depth > MaxRecursionDepth)
                return false; // Malformed file — bail to prevent stack overflow

            try
            {
                while (stream.Position < stream.Length)
                {
                    int typeByte = stream.ReadByte();
                    if (typeByte < 0) return false; // EOF
                    if (typeByte == KvTypeEnd) return false; // End of this subtree — return to parent

                    string name = ReadNullTerminatedUtf8(stream);
                    if (stream.Position >= stream.Length && typeByte != KvTypeEnd)
                        return false; // Truncated after name

                    switch (typeByte)
                    {
                        case KvTypeNone: // Subtree — recurse into children
                        {
                            // When entering a "bits" subtree, set the flag so child
                            // permission nodes are recognized as achievement permissions
                            bool childInsideBits = insideBits
                                || string.Equals(name, "bits", StringComparison.OrdinalIgnoreCase);
                            if (ScanKvTree(stream, depth + 1, childInsideBits))
                                return true;
                            break;
                        }

                        case KvTypeString: // Null-terminated UTF-8 string value
                            ReadNullTerminatedUtf8(stream);
                            break;

                        case KvTypeInt32: // 4-byte signed integer
                        {
                            int value = ReadInt32Exact(stream);
                            // Only match "permission" inside achievement nodes (under "bits")
                            if (insideBits
                                && string.Equals(name, "permission", StringComparison.OrdinalIgnoreCase)
                                && (value & 3) != 0)
                            {
                                return true;
                            }
                            break;
                        }

                        case KvTypeFloat32: // 4-byte float
                            SkipExact(stream, 4);
                            break;

                        case KvTypePointer: // 4-byte pointer
                            SkipExact(stream, 4);
                            break;

                        case KvTypeWideString: // Null-terminated UTF-16LE string (rare)
                            SkipWideString(stream);
                            break;

                        case KvTypeColor: // 4-byte RGBA
                            SkipExact(stream, 4);
                            break;

                        case KvTypeUInt64: // 8-byte unsigned integer
                            SkipExact(stream, 8);
                            break;

                        default:
                            // Unknown type code — cannot continue parsing safely
                            return false;
                    }
                }
            }
            catch
            {
                // Any parse error → not protected as far as we can tell
            }
            return false;
        }

        /// <summary>
        /// Reads a null-terminated UTF-8 string from the stream.
        /// Stops at null byte (0x00) or EOF. Returns empty string if first byte is null.
        /// </summary>
        private static string ReadNullTerminatedUtf8(Stream stream)
        {
            var bytes = new List<byte>(64);
            int b;
            while ((b = stream.ReadByte()) > 0)
            {
                bytes.Add((byte)b);
            }
            return bytes.Count == 0 ? "" : Encoding.UTF8.GetString(bytes.ToArray());
        }

        /// <summary>
        /// Reads exactly 4 bytes as a little-endian Int32.
        /// Throws if fewer than 4 bytes available (truncated file).
        /// </summary>
        private static int ReadInt32Exact(Stream stream)
        {
            var buf = new byte[4];
            int totalRead = 0;
            while (totalRead < 4)
            {
                int read = stream.Read(buf, totalRead, 4 - totalRead);
                if (read == 0) throw new EndOfStreamException();
                totalRead += read;
            }
            return BitConverter.ToInt32(buf, 0);
        }

        /// <summary>
        /// Skips exactly <paramref name="count"/> bytes.
        /// Throws if stream doesn't have enough bytes (truncated file).
        /// </summary>
        private static void SkipExact(Stream stream, int count)
        {
            // For seekable streams, use Seek; otherwise read and discard
            if (stream.CanSeek)
            {
                long newPos = stream.Position + count;
                if (newPos > stream.Length) throw new EndOfStreamException();
                stream.Seek(count, SeekOrigin.Current);
            }
            else
            {
                var buf = new byte[count];
                int totalRead = 0;
                while (totalRead < count)
                {
                    int read = stream.Read(buf, totalRead, count - totalRead);
                    if (read == 0) throw new EndOfStreamException();
                    totalRead += read;
                }
            }
        }

        /// <summary>
        /// Skips a null-terminated UTF-16LE (wide) string.
        /// Reads 2 bytes at a time until a null character (0x0000) is found.
        /// </summary>
        private static void SkipWideString(Stream stream)
        {
            var buf = new byte[2];
            while (true)
            {
                int totalRead = 0;
                while (totalRead < 2)
                {
                    int read = stream.Read(buf, totalRead, 2 - totalRead);
                    if (read == 0) return; // EOF
                    totalRead += read;
                }
                if (buf[0] == 0 && buf[1] == 0) return; // Null terminator
            }
        }

        #endregion

        #region Cross-process Mutex

        /// <summary>
        /// RAII wrapper around a named Mutex. Disposes = release + close.
        /// If acquisition fails (timeout, security), all operations are no-ops.
        /// Usage: using (var guard = new MutexGuard()) { ... }
        /// </summary>
        private sealed class MutexGuard : IDisposable
        {
            private Mutex _mutex;
            private bool _owned;

            public MutexGuard()
            {
                try
                {
                    _mutex = new Mutex(false, MutexName);
                    try
                    {
                        _owned = _mutex.WaitOne(MutexTimeoutMs);
                    }
                    catch (AbandonedMutexException)
                    {
                        // Previous holder crashed — we now own the mutex
                        _owned = true;
                    }

                    if (!_owned)
                    {
                        _mutex.Dispose();
                        _mutex = null;
                    }
                }
                catch
                {
                    // Security exception, etc. — proceed without lock
                    _mutex = null;
                    _owned = false;
                }
            }

            public void Dispose()
            {
                if (_mutex != null && _owned)
                {
                    try { _mutex.ReleaseMutex(); } catch { }
                    _owned = false;
                }
                if (_mutex != null)
                {
                    try { _mutex.Dispose(); } catch { }
                    _mutex = null;
                }
            }
        }

        #endregion
    }
}
