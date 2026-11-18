namespace BlackThunder.GeoSystem.Geometry
{
    public interface IPositioned<out TPosition>
    {
        TPosition Position { get; }
    }
}
