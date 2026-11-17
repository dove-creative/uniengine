using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Mathematics;

namespace BlackThunder.GeoSystem.Geometry
{
    public abstract class Segment<P, R> : NavSegment, ISegment<P, R>
    {
        // Front
        public new INode<P> From => (INode<P>)base.From;
        public new INode<P> To => (INode<P>)base.To;
        public new IEnumerable<INode<P>> Nodes => base.Nodes.Cast<INode<P>>();
        public virtual IDictionary<string, float> Values => EmptySegmentInputs<P>.Values;
        public virtual IList<SegmentHandle<P>> Handles => EmptySegmentInputs<P>.Handles;
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
            if (node is not INode<P>)
                throw new ArgumentException("The node must use the same position type as the segment.", nameof(node));
            GeoDependency.Validate(this, node);
        }

        protected virtual void OnToleranceChanged() => OnUpdated((int)GeoEvents.Shape);

        public bool TryGetPosition(Location location, out P position, float? epsilon = null)
        {
            var success = TryGetTransform(location, out position, out _, epsilon);
            if (!success) position = default;
            return success;
        }

        public abstract bool TryGetTransform(Location location, out P position, out R rotation, float? epsilon = null);
    }
}
