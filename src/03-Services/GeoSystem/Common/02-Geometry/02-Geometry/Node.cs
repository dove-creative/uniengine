using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public class Node : NavNode, INode
    {
        // Front
        public Vector2 Position
        {
            get => _position;
            set
            {
                ThrowIfDisposed();
                if (!Location.IsFinite(value.X) || !Location.IsFinite(value.Y))
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (_position == value) return;

                _position = value;
                OnUpdated((int)GeoEvents.Shape);
            }
        }
        public new IEnumerable<ISegment> Segments => base.Segments.Cast<ISegment>();

        // Internal
        private Vector2 _position;


        // Content
        public Node() { }
        public Node(Vector2 position) => Position = position;
        public static implicit operator Vector2(Node node) => node.Position;

        protected override void OnSegmentConnecting(INavSegment segment)
        {
            if (segment is not ISegment)
                throw new ArgumentException("A planar node requires a planar segment.", nameof(segment));
        }
    }
}
