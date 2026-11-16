using System;
using Avalonia;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.UniEngine.Samples.GeoSystem
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 1 && (args[0] == "help" || args[0] == "--help"))
            {
                Console.WriteLine("GeoSystem desktop sample");
                Console.WriteLine("  no arguments : open the interactive Avalonia demo");
                Console.WriteLine("  verify       : verify the POCO scenario without opening a window");
                return 0;
            }
            if (args.Length > 1 || (args.Length == 1 && args[0] != "verify"))
            {
                Console.Error.WriteLine("Unknown command. Use help or verify, or run without arguments.");
                return 1;
            }

            try
            {
                Bootstrapper.Initialize();
                if (args.Length == 1)
                {
                    SceneVerification.Run();
                    return 0;
                }
                return AppBuilder.Configure<DemoApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
            finally
            {
                if (Bootstrapper.IsInitialized) EventStream.Main.Dispose();
            }
        }
    }
}
