namespace BlackThunder.GeoSystem.Geometry
{
    public interface ILinearSegment : ISegment { }
    public interface ILinearSegment<P, R> : ISegment<P, R>, ILinearSegment { }
}
