using System;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.UniEngine.Samples
{
    public static class HandleTokenSample
    {
        // Content
        public static void Run()
        {
            // A handle runs its cleanup once, on the first Dispose.
            var lease = new Handle(() => Console.WriteLine("Handle: texture lease released"))
            {
                Description = "Texture lease"
            };

            lease.Dispose();
            lease.Dispose();
            Console.WriteLine($"{lease.Description} disposed: {lease.IsDisposed}");

            // A token collects cleanup actions from several owners and runs them together.
            var scene = new Token();
            scene.Action += () => Console.WriteLine("Token: music stopped");
            scene.Action += () => throw new InvalidOperationException("Menu cleanup failed.");
            scene.Action += () => Console.WriteLine("Token: input released");

            // A failing action is reported, and the remaining actions still run.
            scene.Dispose();

            try
            {
                scene.Action += () => Console.WriteLine("Never registered");
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("Registering on a disposed token is rejected (expected).");
            }
        }
    }
}
