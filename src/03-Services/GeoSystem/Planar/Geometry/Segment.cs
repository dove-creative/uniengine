using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.UniEngine.Mathematics;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public abstract class Segment : NavSegment, ISegment
    {
        // Front
        public new INode From => (INode)base.From;
        public new INode To => (INode)base.To;
        public new IEnumerable<INode> Nodes => base.Nodes.Cast<INode>();
        public IDictionary<string, float> Values => EmptySegmentInputs<Vector2>.Values;
        public IList<SegmentHandle<Vector2>> Handles => EmptySegmentInputs<Vector2>.Handles;
        public float Epsilon
        {
            get => _epsilon;
            set
            {
                ThrowIfDisposed();
                Location.ValidateEpsilon(value);
                if (_epsilon == value) return;
                _epsilon = value;
                OnToleranceChanged();
            }
        }

        // Internal
        private float _epsilon = UniMath.Epsilon;


        // Content
        protected Segment() : base(0f) { }

        protected override void OnNodeConnecting(ConnectionSide side, INavNode node)
        {
            if (node is not INode)
                throw new ArgumentException("A planar segment requires a planar node.", nameof(node));
            GeoDependency.Validate(this, node);
        }

        protected virtual void OnToleranceChanged() => OnUpdated((int)GeoEvents.Shape);

        public bool TryGetPosition(Location location, out Vector2 position, float? epsilon = null)
        {
            var success = TryGetTransform(location, out var transform, epsilon);
            position = success ? transform.Position : default;
            return success;
        }

        public abstract bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null);
    }
}
