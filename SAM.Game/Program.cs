/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace SAM.Game
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                var name = new AssemblyName(e.Name).Name + ".dll";
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", name);
                if (File.Exists(path) == false)
                {
                    return null;
                }

                try
                {
                    return Assembly.LoadFrom(path);
                }
                catch (FileLoadException)
                {
                    // Some users launch from ZIP/OneDrive extracted folders where MOTW blocks LoadFrom.
                }
                catch (NotSupportedException)
                {
                    // .NET can reject assemblies treated as remote sources.
                }
                catch (SecurityException)
                {
                    // Fallback to in-memory load when file trust metadata blocks direct load.
                }

                try
                {
                    return Assembly.Load(File.ReadAllBytes(path));
                }
                catch
                {
                    return null;
                }
            };

            long appId;

            if (args.Length == 0)
            {
                Process.Start("SAM.Picker.exe");
                return;
            }

            if (long.TryParse(args[0], out appId) == false)
            {
                MessageBox.Show(
                    "Could not parse application ID from command line argument.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Run the actual application logic in a separate method so that
            // the JIT does not try to resolve Serilog before AssemblyResolve is registered.
            Run(args, appId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Run(string[] args, long appId)
        {
            // Initialize logging with appId in filename
            LogSetup.Initialize(appId);

            Serilog.Log.Information("=== SAM.Game starting === AppId: {AppId}, Args: {Args}",
                appId, string.Join(" ", args));
            Serilog.Log.Information("Version: {Version}, .NET: {Runtime}, OS: {OS}",
                Assembly.GetExecutingAssembly().GetName().Version,
                Environment.Version,
                Environment.OSVersion);

            // Global exception handler
            AppDomain.CurrentDomain.UnhandledException += (s, ue) =>
            {
                Serilog.Log.Fatal(ue.ExceptionObject as Exception, "Unhandled domain exception");
                Serilog.Log.CloseAndFlush();
            };

            if (API.Steam.GetInstallPath() == Application.StartupPath)
            {
                Serilog.Log.Error("Attempted to run from Steam directory: {Path}", Application.StartupPath);
                MessageBox.Show(
                    "This tool declines to being run from the Steam directory.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                LogSetup.Shutdown();
                return;
            }

            bool unlockAll = args.Length > 1 && args[1] == "--unlock-all";
            bool idle = args.Length > 1 && args[1] == "--idle";
            double idleHours = 0;
            if (idle && args.Length > 2 && args[2].StartsWith("--hours="))
            {
                double.TryParse(args[2].Substring(8),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out idleHours);
            }
            bool headless = unlockAll || idle;

            Serilog.Log.Information("Mode: {Mode}, IdleHours: {Hours}",
                unlockAll ? "unlock-all" : idle ? "idle" : "GUI",
                idleHours);

            // Extended diagnostics for Issue #2 ("failed to create pipe")
            Serilog.Log.Debug("--- Diagnostic snapshot ---");
            Serilog.Log.Debug("Executable: {Path}", Assembly.GetExecutingAssembly().Location);
            Serilog.Log.Debug("WorkingDirectory: {Cwd}", Environment.CurrentDirectory);
            Serilog.Log.Debug("SteamAppId env: {Value}",
                Environment.GetEnvironmentVariable("SteamAppId") ?? "(not set)");
            Serilog.Log.Debug("Steam install: {Path}", API.Steam.GetInstallPath() ?? "(null)");
            Serilog.Log.Debug("IsElevated: {Elevated}", IsProcessElevated());
            try
            {
                using (var current = Process.GetCurrentProcess())
                using (var parent = ParentProcessHelper.GetParentProcess(current))
                {
                    Serilog.Log.Debug("ParentProcess: {Name} (PID {Pid})",
                        parent?.ProcessName ?? "(unknown)", parent?.Id ?? -1);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug("ParentProcess: could not determine ({Error})", ex.Message);
            }
            Serilog.Log.Debug("--- End diagnostic snapshot ---");

            using (API.Client client = new())
            {
                try
                {
                    client.Initialize(appId);
                    Serilog.Log.Information("Steam client initialized for app {AppId}", appId);
                }
                catch (API.ClientInitializeException e)
                {
                    Serilog.Log.Error(e, "Steam client init failed for app {AppId}: {Failure}", appId, e.Failure);
                    if (headless)
                    {
                        Console.Error.WriteLine($"Failed to initialize: {e.Message}");
                        Environment.ExitCode = 1;
                        LogSetup.Shutdown();
                        return;
                    }

                    string errorMsg;
                    if (e.Failure == API.ClientInitializeFailure.CreateSteamPipe)
                    {
                        errorMsg =
                            "Failed to connect to Steam client.\n\n" +
                            "Possible solutions:\n" +
                            "- Run SAM from a local folder (not OneDrive or cloud storage)\n" +
                            "- Make sure SAM and Steam run with the same privileges\n" +
                            "- Close other SAM instances and try again\n" +
                            "- Restart Steam and try again\n\n" +
                            "(" + e.Message + ")";
                    }
                    else if (e.Failure == API.ClientInitializeFailure.ConnectToGlobalUser)
                    {
                        errorMsg =
                            "Steam is not running. Please start Steam then run this tool again.\n\n" +
                            "If you have the game through Family Share, the game may be locked due to\n" +
                            "the Family Share account actively playing a game.\n\n" +
                            "(" + e.Message + ")";
                    }
                    else if (string.IsNullOrEmpty(e.Message) == false)
                    {
                        errorMsg =
                            "Steam is not running. Please start Steam then run this tool again.\n\n" +
                            "(" + e.Message + ")";
                    }
                    else
                    {
                        errorMsg = "Steam is not running. Please start Steam then run this tool again.";
                    }
                    MessageBox.Show(errorMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    LogSetup.Shutdown();
                    return;
                }
                catch (DllNotFoundException ex)
                {
                    Serilog.Log.Fatal(ex, "Steam DLL not found for app {AppId}", appId);
                    if (headless)
                    {
                        Console.Error.WriteLine("DLL not found error.");
                        Environment.ExitCode = 1;
                        LogSetup.Shutdown();
                        return;
                    }

                    MessageBox.Show(
                        "You've caused an exceptional error!",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    LogSetup.Shutdown();
                    return;
                }

                if (unlockAll)
                {
                    Serilog.Log.Information("Starting headless unlock-all for app {AppId}", appId);
                    bool result = RunHeadlessUnlockAll(appId, client);
                    Serilog.Log.Information("Headless unlock-all result: {Result}", result ? "success" : "failed");
                    Environment.ExitCode = result ? 0 : 1;
                    LogSetup.Shutdown();
                    return;
                }

                if (idle)
                {
                    Serilog.Log.Information("Starting idle for app {AppId}, hours: {Hours}", appId, idleHours);
                    bool result = RunIdle(appId, client, idleHours);
                    Serilog.Log.Information("Idle complete for app {AppId}, result: {Result}", appId, result ? "success" : "failed");
                    Environment.ExitCode = result ? 0 : 1;
                    LogSetup.Shutdown();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Serilog.Log.Information("Launching Manager UI for app {AppId}", appId);
                Application.Run(new Manager(appId, client));
            }

            Serilog.Log.Information("=== SAM.Game shutting down ===");
            LogSetup.Shutdown();
        }

        private static bool RunHeadlessUnlockAll(long appId, API.Client client)
        {
            // Request user stats
            var steamId = client.SteamUser.GetSteamId();
            var callHandle = client.SteamUserStats.RequestUserStats(steamId);
            if (callHandle == API.CallHandle.Invalid)
            {
                Serilog.Log.Error("RequestUserStats returned invalid handle for app {AppId}", appId);
                return false;
            }

            // Poll callbacks until stats are received (max 10 seconds)
            bool statsReceived = false;
            int statsResult = -1;
            var callback = client.CreateAndRegisterCallback<API.Callbacks.UserStatsReceived>();
            callback.OnRun += (param) =>
            {
                statsResult = param.Result;
                statsReceived = true;
            };

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!statsReceived && DateTime.UtcNow < deadline)
            {
                client.RunCallbacks(false);
                Thread.Sleep(100);
            }

            if (!statsReceived || statsResult != 1)
            {
                Serilog.Log.Error("Stats not received or failed for app {AppId}: received={Received}, result={Result}",
                    appId, statsReceived, statsResult);
                return false;
            }

            // Enumerate achievements using API
            uint numAchievements = client.SteamUserStats.GetNumAchievements();
            Serilog.Log.Information("Found {Count} achievements for app {AppId}", numAchievements, appId);
            if (numAchievements == 0)
            {
                return true; // no achievements = success
            }

            int unlocked = 0;
            for (uint i = 0; i < numAchievements; i++)
            {
                string name = client.SteamUserStats.GetAchievementName(i);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (client.SteamUserStats.GetAchievement(name, out bool isAchieved) && isAchieved)
                {
                    continue; // already unlocked
                }

                if (client.SteamUserStats.SetAchievement(name, true))
                {
                    Serilog.Log.Debug("Unlocked achievement: {Name}", name);
                    unlocked++;
                }
            }

            Serilog.Log.Information("Unlock-all: {Unlocked} achievements modified for app {AppId}", unlocked, appId);

            if (unlocked > 0)
            {
                return client.SteamUserStats.StoreStats();
            }

            return true;
        }

        private static bool RunIdle(long appId, API.Client client, double hours)
        {
            Console.WriteLine($"Idling app {appId}...");
            if (hours > 0)
            {
                Console.WriteLine($"Will idle for {hours:F1} hour(s).");
            }
            else
            {
                Console.WriteLine("Idling indefinitely. Press Ctrl+C or kill process to stop.");
            }

            var startTime = DateTime.UtcNow;
            var endTime = hours > 0
                ? startTime.AddHours(hours)
                : DateTime.MaxValue;

            bool cancelled = false;
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cancelled = true;
                Serilog.Log.Information("Idle cancelled by Ctrl+C for app {AppId}", appId);
            };

            // Named event for graceful shutdown from ActiveGamesForm
            string eventName = $"Local\\SAM_Idle_Stop_{appId}";
            using var stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);

            while (!cancelled && DateTime.UtcNow < endTime)
            {
                // Wait 5 seconds or until stop event is signaled
                if (stopEvent.WaitOne(5000))
                {
                    Serilog.Log.Information("Idle received stop signal for app {AppId}", appId);
                    Console.WriteLine("\nReceived stop signal.");
                    break;
                }

                client.RunCallbacks(false);

                if (hours > 0)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    var remaining = endTime - DateTime.UtcNow;
                    if (remaining.TotalSeconds > 0)
                    {
                        Console.Write($"\rElapsed: {elapsed.TotalHours:F2}h / {hours:F1}h   ");
                    }
                }
            }

            Console.WriteLine();
            var total = DateTime.UtcNow - startTime;
            Serilog.Log.Information("Idle complete for app {AppId}: {TotalHours:F2} hours", appId, total.TotalHours);
            Console.WriteLine($"Idle complete. Total time: {total.TotalHours:F2} hours.");
            return true;
        }

        private static bool IsProcessElevated()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class ParentProcessHelper
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [System.Runtime.InteropServices.DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle, int processInformationClass,
            ref PROCESS_BASIC_INFORMATION processInformation,
            int processInformationLength, out int returnLength);

        public static Process GetParentProcess(Process process)
        {
            try
            {
                var pbi = new PROCESS_BASIC_INFORMATION();
                int status = NtQueryInformationProcess(
                    process.Handle, 0, ref pbi,
                    System.Runtime.InteropServices.Marshal.SizeOf(pbi), out _);
                if (status != 0) return null;

                int parentPid = pbi.InheritedFromUniqueProcessId.ToInt32();
                if (parentPid <= 0) return null;
                return Process.GetProcessById(parentPid);
            }
            catch
            {
                return null;
            }
        }
    }
}
