using System;

namespace BlackThunder.GeoSystem.Navigation
{
    [Flags]
    public enum GeoEvents
    {
        None = 0,
        Connect = 1,
        Disconnect = 2,
        Shape = 4,
        Traversal = 16,
        AnyConnection = Connect | Disconnect,
        All = Connect | Disconnect | Shape | Traversal,
    }
}
