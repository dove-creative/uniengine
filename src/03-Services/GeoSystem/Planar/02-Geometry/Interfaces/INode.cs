using System.Collections.Generic;
using System.Numerics;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public interface IPositioned : Geometry.IPositioned<Vector2> { }

    public interface IReadOnlyNode : Geometry.IReadOnlyNode<Vector2>, IPositioned
    {
        new IEnumerable<IReadOnlySegment> Segments { get; }
        IEnumerable<Geometry.IReadOnlySegment<Vector2>> Geometry.IReadOnlyNode<Vector2>.Segments => Segments;
    }

    public interface INode : Geometry.INode<Vector2>, IReadOnlyNode
    {
        new IEnumerable<ISegment> Segments { get; }
        IEnumerable<IReadOnlySegment> IReadOnlyNode.Segments => Segments;
        IEnumerable<Geometry.ISegment<Vector2>> Geometry.INode<Vector2>.Segments => Segments;
    }
}
