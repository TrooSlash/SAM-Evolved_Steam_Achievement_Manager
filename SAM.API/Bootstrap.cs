using System;
using System.IO;
using System.Reflection;
using System.Security;

namespace SAM.API
{
    public static class Bootstrap
    {
        public static void RegisterAssemblyResolver()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromLib;
        }

        private static Assembly ResolveFromLib(object sender, ResolveEventArgs e)
        {
            var name = new AssemblyName(e.Name).Name + ".dll";
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", name);
            if (File.Exists(path) == false)
                return null;

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
        }
    }
}
