using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.UniEngine.Samples
{
    public static class AdaptiveCollectionsSample
    {
        // Content
        public static void Run()
        {
            AddWhileEnumerating();
            RemoveWhileEnumerating();
            InvokeListeners();
            RemoveExpiredEntries();

            // Deferred addition is the default: an item added during a pass is visited from the next pass.
            void AddWhileEnumerating()
            {
                var deferred = new AdaptiveCollection<string> { "A", "B" };
                var immediate = new AdaptiveCollection<string>(deferredUpdate: false) { "A", "B" };
                Console.WriteLine($"Deferred pass visits: {AddDuringPass(deferred)}, count after: {deferred.Count}");
                Console.WriteLine($"Immediate pass visits: {AddDuringPass(immediate)}, count after: {immediate.Count}");
            }

            static string AddDuringPass(AdaptiveCollection<string> collection)
            {
                var visited = new List<string>();
                foreach (var item in collection)
                {
                    visited.Add(item);
                    if (item == "A") collection.Add("C");
                }

                return string.Join(", ", visited);
            }

            // A removed item is skipped for the rest of the pass.
            void RemoveWhileEnumerating()
            {
                var collection = new AdaptiveCollection<string> { "A", "B", "C" };
                var visited = new List<string>();
                foreach (var item in collection)
                {
                    visited.Add(item);
                    if (item == "A") collection.Remove("B");
                }

                Console.WriteLine($"Pass that removes B visits: {string.Join(", ", visited)}");
            }

            // AdaptiveAction lets a listener remove itself and drops a listener that throws.
            void InvokeListeners()
            {
                var ticked = new AdaptiveAction();
                Action once = null;

                once = () =>
                {
                    Console.WriteLine("Listener: runs only once");
                    ticked.Remove(once);
                };

                ticked.Add(once);
                ticked.Add(() => Console.WriteLine("Listener: runs every tick"));
                ticked.Add(() => throw new InvalidOperationException("Broken listener."));

                ticked.Invoke();
                ticked.Invoke();
                Console.WriteLine($"Listeners left: {ticked.Count}");
            }

            // AdaptiveDictionary allows removing entries while enumerating it.
            void RemoveExpiredEntries()
            {
                var cooldowns = new AdaptiveDictionary<string, int> { { "Dash", 0 }, { "Heal", 2 }, { "Jump", 0 } };
                foreach (var cooldown in cooldowns)
                {
                    if (cooldown.Value == 0) cooldowns.Remove(cooldown.Key);
                }

                Console.WriteLine($"Cooldowns left: {string.Join(", ", cooldowns.Keys)}");
            }
        }
    }
}
