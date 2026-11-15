using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using BlackThunder.UniEngine.Objects;
using BlackThunder.UniEngine.Utilities;
using BlackThunder.UniEngine.Utilities.Json;

namespace BlackThunder.UniEngine.Samples
{
    public static class UtilitiesSnippet
    {
        // Content
        public static void FormatForDisplay()
        {
            var count = 1234567.ToCompactString(); // "1.2M"
            var distance = 1530.5f.ToCompactString(); // "1.5K"
            var duration = new TimeSpan(1, 5, 30).ToDisplayString(); // "1h 5m"
            var levels = new List<int> { 1, 2, 3 }.ToDisplayString(detailedFormat: false); // "[1, 2, 3]"
        }

        public static void RoundTripJson()
        {
            // The converters also apply to values held in properties of other objects.
            var options = new JsonSerializerOptions().AddUniEngineConverters();
            var spawnPoint = new SpawnPoint
            {
                Name = "Gate",
                Cell = new Vector2Int(4, 7),
                Pose = new Transform2D(new Vector2(4.5f, 7.5f), 0f)
            };

            var json = JsonSerializer.Serialize(spawnPoint, options);
            // {"Name":"Gate","Cell":{"X":4,"Y":7},"Pose":{"Position":{"X":4.5,"Y":7.5},"Rotation":0}}

            var restored = JsonSerializer.Deserialize<SpawnPoint>(json, options); // the same values as spawnPoint
        }

        private class SpawnPoint
        {
            public string Name { get; set; }
            public Vector2Int Cell { get; set; }
            public Transform2D Pose { get; set; }
        }
    }
}
