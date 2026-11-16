using System.Numerics;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public interface IPositioned
    {
        Vector2 Position { get; }
    }

    public interface IReadOnlyNode : Geometry.IReadOnlyNode, IPositioned { }
    public interface INode : Geometry.INode, IReadOnlyNode { }
}
