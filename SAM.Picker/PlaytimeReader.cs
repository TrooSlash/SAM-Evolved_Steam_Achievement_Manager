using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SAM.Game;
using Serilog;

namespace SAM.Picker
{
    internal struct AppLocalData
    {
        public int PlaytimeMinutes;
        public long LastPlayedTimestamp;
    }

    /// <summary>
    /// Reads playtime and last-played data from Steam's localconfig.vdf file.
    /// </summary>
    internal static class PlaytimeReader
    {
        /// <summary>
        /// Returns a dictionary of AppId → AppLocalData (playtime + last played).
        /// </summary>
        public static Dictionary<uint, AppLocalData> Read(ulong steamId64)
        {
            var result = new Dictionary<uint, AppLocalData>();

            string steamPath = API.Steam.GetInstallPath();
            if (string.IsNullOrEmpty(steamPath))
                return result;

            uint accountId = (uint)(steamId64 & 0xFFFFFFFF);

            string configPath = Path.Combine(steamPath, "userdata",
                accountId.ToString(), "config", "localconfig.vdf");

            if (!File.Exists(configPath))
                return result;

            try
            {
                string content = File.ReadAllText(configPath);
                var parsed = ParseVdf(content);
                foreach (var kv in parsed)
                    result[kv.Key] = kv.Value;
                Log.Debug("PlaytimeReader parsed {Count} app entries from localconfig.vdf", result.Count);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to parse playtime data from {ConfigPath}", configPath);
            }

            return result;
        }

        internal static Dictionary<uint, AppLocalData> ParseVdf(string content)
        {
            var result = new Dictionary<uint, AppLocalData>();

            var root = KeyValue.ParseText(content);
            if (root == null || !root.Valid)
                return result;

            var apps = FindAppsNode(root);
            if (apps == null || apps.Children == null)
                return result;

            foreach (var app in apps.Children)
            {
                if (!uint.TryParse(app.Name, out uint appId))
                    continue;

                var playtime = app["Playtime"];
                if (!playtime.Valid)
                    playtime = app["playtime_forever"];

                int pt = playtime.AsInteger(0);

                long lp = 0;
                var lastPlayed = app["LastPlayed"];
                if (lastPlayed.Valid)
                    long.TryParse(lastPlayed.AsString("0"), out lp);

                if (pt >= 0 || lp > 0)
                {
                    result[appId] = new AppLocalData
                    {
                        PlaytimeMinutes = pt,
                        LastPlayedTimestamp = lp
                    };
                }
            }

            return result;
        }

        private static KeyValue FindAppsNode(KeyValue root)
        {
            if (string.Equals(root.Name, "apps", StringComparison.OrdinalIgnoreCase))
                return root;

            var apps = root["Software"]["Valve"]["Steam"]["apps"];
            if (apps.Valid)
                return apps;

            return null;
        }

        public static string FormatPlaytime(int minutes)
        {
            if (minutes <= 0) return "—";
            if (minutes < 60)
                return $"{minutes} min";
            double hours = minutes / 60.0;
            return string.Format(CultureInfo.InvariantCulture, "{0:F1} hrs", hours);
        }

        public static string FormatLastPlayed(long unixTimestamp)
        {
            if (unixTimestamp <= 0) return "—";
            try
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).LocalDateTime;
                var diff = DateTime.Now - dt;
                if (diff.TotalMinutes < 60) return "Just now";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
                if (diff.TotalDays < 365) return dt.ToString("dd MMM");
                return dt.ToString("dd.MM.yyyy");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to format last played timestamp {Timestamp}", unixTimestamp);
                return "—";
            }
        }
    }
}
