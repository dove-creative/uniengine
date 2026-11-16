using System.Collections.Generic;
using System.Numerics;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public interface IReadOnlySegment : Geometry.IReadOnlySegment
    {
        new IReadOnlyNode From { get; }
        new IReadOnlyNode To { get; }
        new IEnumerable<IReadOnlyNode> Nodes { get; }
        Geometry.IReadOnlyNode Geometry.IReadOnlySegment.From => From;
        Geometry.IReadOnlyNode Geometry.IReadOnlySegment.To => To;
        IEnumerable<Geometry.IReadOnlyNode> Geometry.IReadOnlySegment.Nodes => Nodes;

        bool TryGetPosition(Location location, out Vector2 position, float? epsilon = null);
        bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null);
    }

    public interface ISegment : Geometry.ISegment, IReadOnlySegment
    {
        new INode From { get; }
        new INode To { get; }
        new IEnumerable<INode> Nodes { get; }
        IReadOnlyNode IReadOnlySegment.From => From;
        IReadOnlyNode IReadOnlySegment.To => To;
        IEnumerable<IReadOnlyNode> IReadOnlySegment.Nodes => Nodes;
        Geometry.INode Geometry.ISegment.From => From;
        Geometry.INode Geometry.ISegment.To => To;
        IEnumerable<Geometry.INode> Geometry.ISegment.Nodes => Nodes;
        Geometry.IReadOnlyNode Geometry.IReadOnlySegment.From => From;
        Geometry.IReadOnlyNode Geometry.IReadOnlySegment.To => To;
        IEnumerable<Geometry.IReadOnlyNode> Geometry.IReadOnlySegment.Nodes => Nodes;
        INavNode INavSegment.From => From;
        INavNode INavSegment.To => To;
        IEnumerable<INavNode> INavSegment.Nodes => Nodes;
        IReadOnlyNavNode IReadOnlyNavSegment.From => From;
        IReadOnlyNavNode IReadOnlyNavSegment.To => To;
        IEnumerable<IReadOnlyNavNode> IReadOnlyNavSegment.Nodes => Nodes;
    }

    public interface ILinearSegment : ISegment, Geometry.ILinearSegment { }
}
