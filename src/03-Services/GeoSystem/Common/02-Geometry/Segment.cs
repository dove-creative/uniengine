using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Mathematics;

namespace BlackThunder.GeoSystem.Geometry
{
    public abstract class Segment<TPosition, TRotation> : NavSegment, ISegment<TPosition, TRotation>
    {
        // Front
        public new INode<TPosition> From => (INode<TPosition>)base.From;
        public new INode<TPosition> To => (INode<TPosition>)base.To;
        public new IEnumerable<INode<TPosition>> Nodes => base.Nodes.Cast<INode<TPosition>>();
        public virtual IDictionary<string, float> Values => EmptySegmentInputs<TPosition>.Values;
        public virtual IList<ControlHandle<TPosition>> Handles => EmptySegmentInputs<TPosition>.Handles;

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
            if (node is not INode<TPosition>)
                throw new ArgumentException("The node must use the same position type as the segment.", nameof(node));
            GeoDependency.Validate(this, node);
        }

        protected virtual void OnToleranceChanged() => OnUpdated((int)GeoEvents.Shape);

        public bool TryGetPosition(Location location, out TPosition position, float? epsilon = null)
        {
            var success = TryGetTransform(location, out position, out _, epsilon);
            if (!success) position = default;
            return success;
        }

        public abstract bool TryGetTransform(
            Location location,
            out TPosition position,
            out TRotation rotation,
            float? epsilon = null);
    }
}
