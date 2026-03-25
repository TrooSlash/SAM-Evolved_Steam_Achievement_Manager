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
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace SAM.Picker
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            API.Bootstrap.RegisterAssemblyResolver();

            // Run the actual application logic in a separate method so that
            // the JIT does not try to resolve Serilog before AssemblyResolve is registered.
            Run();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Run()
        {
            // Initialize settings and logging early
            AppSettings.Load();
            LogSetup.Initialize();

            Serilog.Log.Information("=== SAM.Picker starting ===");
            Serilog.Log.Information("Version: {Version}, .NET: {Runtime}, OS: {OS}",
                Assembly.GetExecutingAssembly().GetName().Version,
                Environment.Version,
                Environment.OSVersion);

            // Global exception handlers
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Serilog.Log.Fatal(args.ExceptionObject as Exception, "Unhandled domain exception");
                Serilog.Log.CloseAndFlush();
            };
            Application.ThreadException += (s, args) =>
            {
                Serilog.Log.Error(args.Exception, "Unhandled UI thread exception");
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

            using (API.Client client = new())
            {
                try
                {
                    client.Initialize(0);
                    // Clear SteamAppId so child processes (SAM.Game) don't inherit "0"
                    Environment.SetEnvironmentVariable("SteamAppId", null);
                    Serilog.Log.Information("Steam client initialized successfully");
                }
                catch (API.ClientInitializeException e)
                {
                    Serilog.Log.Error(e, "Steam client initialization failed: {Failure}", e.Failure);
                    if (string.IsNullOrEmpty(e.Message) == false)
                    {
                        MessageBox.Show(
                            "Steam is not running. Please start Steam then run this tool again.\n\n" +
                            "(" + e.Message + ")",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    else
                    {
                        MessageBox.Show(
                            "Steam is not running. Please start Steam then run this tool again.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    LogSetup.Shutdown();
                    return;
                }
                catch (DllNotFoundException ex)
                {
                    Serilog.Log.Fatal(ex, "Steam DLL not found");
                    MessageBox.Show(
                        "You've caused an exceptional error!",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    LogSetup.Shutdown();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Serilog.Log.Information("Launching GamePicker UI");
                Application.Run(new GamePicker(client));
            }

            Serilog.Log.Information("=== SAM.Picker shutting down ===");
            LogSetup.Shutdown();
        }
    }
}
