namespace BlackThunder.GeoSystem.Geometry
{
    public interface IPositioned<out P>
    {
        P Position { get; }
    }
}
