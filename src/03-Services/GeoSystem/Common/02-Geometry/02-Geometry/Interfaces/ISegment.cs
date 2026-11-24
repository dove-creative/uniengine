using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public interface IReadOnlySegment : Geometry.IReadOnlySegment<Vector2, float>
    {
        new IReadOnlyNode From { get; }
        new IReadOnlyNode To { get; }
        new IEnumerable<IReadOnlyNode> Nodes { get; }
        Geometry.IReadOnlyNode<Vector2> Geometry.IReadOnlySegment<Vector2>.From => From;
        Geometry.IReadOnlyNode<Vector2> Geometry.IReadOnlySegment<Vector2>.To => To;
        IEnumerable<Geometry.IReadOnlyNode<Vector2>> Geometry.IReadOnlySegment<Vector2>.Nodes => Nodes;

        bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null);

        bool Geometry.IReadOnlySegment<Vector2, float>.TryGetTransform(
            Location location,
            out Vector2 position,
            out float rotation,
            float? epsilon)
        {
            var success = TryGetTransform(location, out var transform, epsilon);
            position = success ? transform.Position : default;
            rotation = success ? transform.Rotation : default;
            return success;
        }
    }

    public interface ISegment : Geometry.ISegment<Vector2, float>, IReadOnlySegment
    {
        new INode From { get; }
        new INode To { get; }
        new IEnumerable<INode> Nodes { get; }
        IReadOnlyNode IReadOnlySegment.From => From;
        IReadOnlyNode IReadOnlySegment.To => To;
        IEnumerable<IReadOnlyNode> IReadOnlySegment.Nodes => Nodes;
        Geometry.INode<Vector2> Geometry.ISegment<Vector2>.From => From;
        Geometry.INode<Vector2> Geometry.ISegment<Vector2>.To => To;
        IEnumerable<Geometry.INode<Vector2>> Geometry.ISegment<Vector2>.Nodes => Nodes;
    }
}
