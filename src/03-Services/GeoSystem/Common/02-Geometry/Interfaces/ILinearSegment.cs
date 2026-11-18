namespace BlackThunder.GeoSystem.Geometry
{
    public interface ILinearSegment : ISegment { }
    public interface ILinearSegment<TPosition, TRotation> : ISegment<TPosition, TRotation>, ILinearSegment { }
}
