using System;
using System.IO;
using System.Reflection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SAM.Picker
{
    internal static class LogSetup
    {
        private static readonly string BasePath =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static readonly string LogDirectory = Path.Combine(BasePath, "lib", "logs");

        public static readonly LoggingLevelSwitch LevelSwitch = new(LogEventLevel.Debug);

        public static void Initialize()
        {
            if (Enum.TryParse<LogEventLevel>(AppSettings.LogLevel, true, out var level))
                LevelSwitch.MinimumLevel = level;

            Directory.CreateDirectory(LogDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LevelSwitch)
                .WriteTo.File(
                    path: Path.Combine(LogDirectory, "sam-picker-.log"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    retainedFileCountLimit: 7,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    shared: false)
                .CreateLogger();

            // Wire SAM.API logging delegates to Serilog
            API.Logging.ApiLogger.Debug = msg => Log.Debug("[API] {Message}", msg);
            API.Logging.ApiLogger.Info = msg => Log.Information("[API] {Message}", msg);
            API.Logging.ApiLogger.Warning = msg => Log.Warning("[API] {Message}", msg);
            API.Logging.ApiLogger.Error = (msg, ex) => Log.Error(ex, "[API] {Message}", msg);
        }

        public static void Shutdown()
        {
            Log.CloseAndFlush();
        }
    }
}
