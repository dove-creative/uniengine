using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Geometry
{
    public interface IPolySegment : ISegment { }
    public interface IPolySegment<P, R> : ISegment<P, R>, IPolySegment
    {
        IShaper<P> Shaper { get; set; }
        IReadOnlyList<Hinge<P, R>> Hinges { get; }
    }
}
