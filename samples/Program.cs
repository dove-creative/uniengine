using System;
using System.Linq;

namespace BlackThunder.UniEngine.Samples
{
    public static class Program
    {
        // Internal
        private static readonly Command[] _commands =
        {
            new("handle-token", "Runs cleanup once when a handle or token is disposed.", HandleTokenSample.Run),
            new("adaptive-collections", "Changes collections during enumeration.", AdaptiveCollectionsSample.Run),
            new("event-stream", "Filters, orders, and branches event streams.", EventStreamSample.Run),
            new("property", "Stores external payloads on a domain object.", PropertySample.Run),
            new("lifecycle-hub", "Notifies connected listeners when an object is disposed.", LifecycleHubSample.Run),
            new("event-hub", "Connects listeners to updates, sleep, and disposal.", EventHubSample.Run),
            new("usm", "Combines state transitions, behavior trees, replays, and parallel children.", UsmSample.Run),
            new("all", "Runs every sample in order.", RunAll)
        };


        // Content
        public static int Main(string[] args)
        {
            SampleEnvironment.Initialize();
            try
            {
                if (args.Length > 0) return RunOnce(args[0], args.Skip(1).ToArray());

                RunInteractive();
                return 0;
            }
            finally
            {
                SampleEnvironment.Shutdown();
            }
        }

        private static int RunOnce(string name, string[] arguments)
        {
            try
            {
                return Execute(name, arguments) ? 0 : 1;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void RunInteractive()
        {
            PrintCommands();

            while (true)
            {
                Console.Write("sample> ");
                var line = Console.ReadLine();
                if (line == null) return;

                var tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0) continue;
                if (tokens[0] == "exit") return;

                try
                {
                    Execute(tokens[0], tokens.Skip(1).ToArray());
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(exception);
                }
            }
        }

        private static bool Execute(string name, string[] arguments)
        {
            if (name == "exit") return true;

            if (name == "help")
            {
                PrintCommands();
                return true;
            }

            var command = Array.Find(_commands, candidate => candidate.Name == name);
            if (command == null)
            {
                Console.Error.WriteLine($"Unknown command: {name}");
                PrintCommands();
                return false;
            }

            if (arguments.Length > 0)
            {
                Console.Error.WriteLine($"The {name} command takes no arguments.");
                return false;
            }

            command.Run();
            return true;
        }

        private static void RunAll()
        {
            foreach (var command in _commands)
            {
                if (command.Run == RunAll) continue;

                Console.WriteLine($"== {command.Name} ==");
                command.Run();
                Console.WriteLine();
            }
        }

        private static void PrintCommands()
        {
            Console.WriteLine("Commands:");

            foreach (var command in _commands)
                Console.WriteLine($"  {command.Name,-22}{command.Description}");

            Console.WriteLine($"  {"help",-22}Shows this list.");
            Console.WriteLine($"  {"exit",-22}Ends the prompt.");
        }

        private class Command
        {
            public string Name { get; }
            public string Description { get; }
            public Action Run { get; }

            public Command(string name, string description, Action run)
            {
                Name = name;
                Description = description;
                Run = run;
            }
        }
    }
}
