using System;

namespace SAM.API.Logging
{
    /// <summary>
    /// Minimal logging abstraction for SAM.API.
    /// Callers inject delegates; the library never references Serilog.
    /// </summary>
    public static class ApiLogger
    {
        public static Action<string> Debug { get; set; } = _ => { };
        public static Action<string> Info { get; set; } = _ => { };
        public static Action<string> Warning { get; set; } = _ => { };
        public static Action<string, Exception> Error { get; set; } = (_, __) => { };
    }
}
