using System;
using System.Globalization;
using System.Resources;

namespace SAM.Game
{
    internal static class GameLocalization
    {
        public enum Language { English, Russian }

        private static Language _current = Language.English;
        public static Language Current { get => _current; set => _current = value; }

        private static readonly ResourceManager _rm =
            new ResourceManager("SAM.Game.GameStrings", typeof(GameLocalization).Assembly);

        private static readonly CultureInfo _enCulture = CultureInfo.InvariantCulture;
        private static readonly CultureInfo _ruCulture = new CultureInfo("ru");

        static GameLocalization()
        {
            string lang = System.Environment.GetEnvironmentVariable("SAM_LANGUAGE");
            if (lang == "Russian") _current = Language.Russian;
        }

        public static string Plural(int count, string one, string few, string many)
        {
            if (_current == Language.English)
                return count == 1 ? one : many;

            int abs = Math.Abs(count) % 100;
            int last = abs % 10;
            if (abs > 10 && abs < 20) return many;
            if (last > 1 && last < 5) return few;
            if (last == 1) return one;
            return many;
        }

        public static string Get(string key)
        {
            var culture = _current == Language.Russian ? _ruCulture : _enCulture;
            return _rm.GetString(key, culture) ?? key;
        }

        public static string Get(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }
    }
}
