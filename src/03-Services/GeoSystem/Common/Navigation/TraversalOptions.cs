using System;

namespace BlackThunder.GeoSystem.Navigation
{
    [Flags]
    public enum TraversalOptions
    {
        None = 0,
        Forward = 1,
        Backward = 2,
        Both = Forward | Backward,
    }
}
