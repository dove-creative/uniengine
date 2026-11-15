using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Core;
using BlackThunder.UniEngine.Infrastructure;

namespace BlackThunder.UniEngine.Samples
{
    public static class LogSystemSnippet
    {
        // Content
        public static void WriteEntries()
        {
            // Log forwards each entry to the logger that the host passes to Bootstrapper.Initialize once.
            Bootstrapper.Initialize(logger: (message, level) => Console.WriteLine($"[{level}] {message}"));

            Log.Write(); // [Log] Called
            "Loading scene".Write(); // [Log] Loading scene
            "Texture is missing".Write(LogLevel.Warning); // [Warning] Texture is missing
            42.Write(); // [Log] 42

            // Collections are written as one entry.
            new List<string> { "Sword", "Shield" }.Write(); // [Log] [Sword, Shield]
            new[] { 1, 2, 3 }.WriteCollection(value => value * 10); // [Log] [10, 20, 30]
        }

        public static void SaveBuffer()
        {
            // Log also keeps entries in a buffer. Print saves it under Configuration.DataPath and clears it.
            var content = Log.Print(); // one '[Level] message' line per entry
            var file = Log.DebugFilePath; // <DataPath>\DebugLog-yyMMdd-HHmmss.txt
        }

        public static void DiscardBuffer() => Log.Reset(); // clears the buffer without writing a file
    }
}
