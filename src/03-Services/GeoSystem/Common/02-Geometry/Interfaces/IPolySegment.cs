namespace BlackThunder.GeoSystem.Geometry
{
    public interface IPolySegment : ISegment { }
    public interface IPolySegment<TPosition, TRotation> : ISegment<TPosition, TRotation>, IPolySegment
    {
        IShaper<TPosition> Shaper { get; set; }
    }
}
