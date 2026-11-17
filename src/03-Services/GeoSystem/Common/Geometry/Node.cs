using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    public class Node<P> : NavNode, INode<P>
    {
        // Front
        public P Position
        {
            get => _position;
            set
            {
                ThrowIfDisposed();
                ValidatePosition(value);
                if (EqualityComparer<P>.Default.Equals(_position, value)) return;
                _position = value;
                OnUpdated((int)GeoEvents.Shape);
            }
        }
        public new IEnumerable<ISegment<P>> Segments => base.Segments.Cast<ISegment<P>>();

        // Internal
        private P _position;


        // Content
        public Node() { }
        public Node(P position) => Position = position;

        protected virtual void ValidatePosition(P position) { }

        protected override void OnSegmentConnecting(INavSegment segment)
        {
            if (segment is not ISegment<P>)
                throw new ArgumentException(
                    "The segment must use the same position type as the node.", nameof(segment));
        }
    }
}
