using System;
using BlackThunder.GeoSystem.Geometry;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public class PolyTracer<TPosition, TRotation> : Tracer<TPosition, TRotation>
    {
        private int _hingeIndex = -1;


        public PolyTracer(Func<TRotation, TRotation, float> turnAngle = null) : base(turnAngle) { }

        protected override bool HasPosition(IReadOnlySegment<TPosition, TRotation> segment, Location location,
            float? epsilon) => segment.GetType() == typeof(Geometry.Planar.PolySegment)
                ? ReadTransform(segment, location, out _, out _, epsilon)
                : base.HasPosition(segment, location, epsilon);

        protected override bool ReadTransform(IReadOnlySegment<TPosition, TRotation> segment, Location location,
            out TPosition position, out TRotation rotation, float? epsilon) =>
            segment is PolySegment<TPosition, TRotation> poly
                ? poly.TryGetTransformWithHint(location, ref _hingeIndex, out position, out rotation, epsilon)
                : base.ReadTransform(segment, location, out position, out rotation, epsilon);
    }
}
