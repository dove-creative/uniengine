using System;

namespace BlackThunder.GeoSystem.Navigation
{
    [Flags]
    public enum GeoEvents
    {
        None = 0,
        Connect = 1 << 0,
        Disconnect = 1 << 1,
        Shape = 1 << 2,
        Traversal = 1 << 4,
        AnyConnection = Connect | Disconnect,
        All = Connect | Disconnect | Shape | Traversal,
    }
}
