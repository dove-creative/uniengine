using System;

namespace BlackThunder.GeoSystem.RouteSystem
{
    [Flags]
    public enum RouteEvents
    {
        None = 0,
        Connect = 1 << 0,
        Disconnect = 1 << 1,
        Shape = 1 << 2,
        Location = 1 << 3,
        Traversal = 1 << 4,
        AnyConnection = Connect | Disconnect,
        All = Connect | Disconnect | Shape | Location | Traversal,
    }
}
