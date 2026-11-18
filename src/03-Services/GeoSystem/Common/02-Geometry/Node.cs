using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    public class Node<TPosition> : NavNode, INode<TPosition>
    {
        // Front
        public TPosition Position
        {
            get => _position;
            set
            {
                ThrowIfDisposed();
                ValidatePosition(value);
                if (EqualityComparer<TPosition>.Default.Equals(_position, value)) return;
                _position = value;
                OnUpdated((int)GeoEvents.Shape);
            }
        }
        public new IEnumerable<ISegment<TPosition>> Segments => base.Segments.Cast<ISegment<TPosition>>();

        // Internal
        private TPosition _position;


        // Content
        public Node() { }
        public Node(TPosition position) => Position = position;

        protected virtual void ValidatePosition(TPosition position) { }

        protected override void OnSegmentConnecting(INavSegment segment)
        {
            if (segment is not ISegment<TPosition>)
                throw new ArgumentException(
                    "The segment must use the same position type as the node.", nameof(segment));
        }
    }
}
