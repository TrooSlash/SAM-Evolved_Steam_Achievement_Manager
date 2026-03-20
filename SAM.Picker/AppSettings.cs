using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SAM.Picker
{
    internal static class AppSettings
    {
        private static readonly string BasePath =
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        private static readonly string SettingsPath = Path.Combine(BasePath, "lib", "sam_settings.ini");

        private static readonly string LegacySettingsPath = Path.Combine(BasePath, "sam_settings.ini");

        // Prefix to identify encrypted values
        private const string EncryptedPrefix = "ENC:";

        public static string SteamApiKey { get; set; } = "";

        public static void Load()
        {
            try
            {
                string path = File.Exists(SettingsPath)
                    ? SettingsPath
                    : LegacySettingsPath;

                if (!File.Exists(path)) return;

                foreach (var line in File.ReadAllLines(path))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2 && parts[0].Trim() == "SteamApiKey")
                    {
                        string value = parts[1].Trim();
                        SteamApiKey = DecryptApiKey(value);
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                string encryptedKey = EncryptApiKey(SteamApiKey);
                File.WriteAllText(SettingsPath, $"SteamApiKey={encryptedKey}\n");
            }
            catch { }
        }

        /// <summary>
        /// Encrypts API key using DPAPI (Windows Data Protection API).
        /// The encrypted data can only be decrypted on the same PC by the same user.
        /// </summary>
        private static string EncryptApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return "";

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(apiKey);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    null, // optional entropy
                    DataProtectionScope.CurrentUser);

                return EncryptedPrefix + Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                // If encryption fails, return plaintext (shouldn't happen normally)
                return apiKey;
            }
        }

        /// <summary>
        /// Decrypts API key using DPAPI.
        /// Supports legacy plaintext keys for migration.
        /// </summary>
        private static string DecryptApiKey(string storedValue)
        {
            if (string.IsNullOrEmpty(storedValue))
                return "";

            // Check if value is encrypted (has ENC: prefix)
            if (storedValue.StartsWith(EncryptedPrefix))
            {
                try
                {
                    string base64 = storedValue.Substring(EncryptedPrefix.Length);
                    byte[] encryptedBytes = Convert.FromBase64String(base64);
                    byte[] plainBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        null, // optional entropy
                        DataProtectionScope.CurrentUser);

                    return Encoding.UTF8.GetString(plainBytes);
                }
                catch
                {
                    // Decryption failed (wrong PC/user or corrupted data)
                    return "";
                }
            }

            // Legacy plaintext key - return as-is (will be encrypted on next save)
            return storedValue;
        }
    }
}
