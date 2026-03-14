using System;
using System.IO;

namespace SAM.Picker
{
    internal static class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "sam_settings.ini");

        public static string SteamApiKey { get; set; } = "";

        public static void Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2 && parts[0].Trim() == "SteamApiKey")
                    {
                        SteamApiKey = parts[1].Trim();
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(SettingsPath, $"SteamApiKey={SteamApiKey}\n");
            }
            catch { }
        }
    }
}
