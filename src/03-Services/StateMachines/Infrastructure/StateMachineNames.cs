using System;

namespace BlackThunder.UniEngine.StateMachines
{
    public static class StateMachineNames
    {
        public static string GetName(object source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var name = source is Type type ? type.Name : source.ToString();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A state machine name cannot be empty.", nameof(source));

            return name;
        }
    }
}
