using System;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.UniEngine.Samples
{
    public static class PropertySample
    {
        // Internal
        private static readonly object _selectionKey = new();
        private static readonly object _pathKey = new();


        // Content
        public static void Run()
        {
            var unit = new Unit("Knight");
            Console.WriteLine($"{unit.Name} has payloads: {(bool)unit.Property}");

            // Each external feature keeps its data under its own key, without adding fields to Unit.
            unit.Property.Add(_selectionKey, true);
            unit.Property.Add(_pathKey, new[] { "Gate", "Bridge" });
            Console.WriteLine($"Selected: {unit.Property.Get<bool>(_selectionKey)}, payloads: {unit.Property.Count}");

            // Add rejects an existing key; the indexer replaces the value.
            try
            {
                unit.Property.Add(_selectionKey, false);
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Adding the same key again is rejected (expected).");
            }

            unit.Property[_selectionKey] = false;
            Console.WriteLine($"Selected after replacement: {unit.Property.Get<bool>(_selectionKey)}");

            // GetAndRemove takes a one-time value out of the store.
            var path = unit.Property.GetAndRemove<string[]>(_pathKey);
            var isStillStored = unit.Property.ContainsKey(_pathKey);
            Console.WriteLine($"Path taken: {string.Join(" -> ", path)}, still stored: {isStillStored}");
            Console.WriteLine($"TryGet after removal: {unit.Property.TryGet<string[]>(_pathKey, out _)}");
        }

        private class Unit : IPropertySubject
        {
            // Front
            public string Name { get; }

            // Property
            public Property Property { get; } = new();


            // Content
            public Unit(string name) => Name = name;
        }
    }
}
