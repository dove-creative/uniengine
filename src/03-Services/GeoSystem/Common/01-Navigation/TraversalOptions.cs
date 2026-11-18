using System;

namespace BlackThunder.GeoSystem.Navigation
{
    [Flags]
    public enum TraversalOptions
    {
        None = 0,
        Forward = 1 << 0,
        Backward = 1 << 1,
        Both = Forward | Backward,
    }
}
