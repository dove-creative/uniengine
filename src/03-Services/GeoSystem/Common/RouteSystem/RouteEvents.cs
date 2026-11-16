using System;

namespace BlackThunder.GeoSystem.RouteSystem
{
    [Flags]
    public enum RouteEvents
    {
        None = 0,
        Connect = 1,
        Disconnect = 2,
        Shape = 4,
        Location = 8,
        AnyConnection = Connect | Disconnect,
        All = Connect | Disconnect | Shape | Location
    }
}
