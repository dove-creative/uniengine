using System;
using System.Collections.Generic;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public class Wheel
    {
        public Queue<int> ReservedRoutes { get; } = new();
        public MoveFailureOption MoveFailureOption
        {
            get => _moveFailureOption;
            set
            {
                if (value != MoveFailureOption.Derailed && value != MoveFailureOption.TrackingFailure)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _moveFailureOption = value;
            }
        }

        private MoveFailureOption _moveFailureOption;


        internal bool TryExit<TPosition, TRotation>(IReadOnlySegment<TPosition, TRotation> incoming,
            bool forward, Func<TRotation, TRotation, float> turnAngle,
            Func<IReadOnlySegment<TPosition, TRotation>, bool, bool> enter)
        {
            var node = forward ? incoming.To : incoming.From;
            if (node == null || node.IsDisposed) return false;

            var candidates = new List<(IReadOnlySegment<TPosition, TRotation> Segment, bool Forward)>();
            foreach (var edge in node.Segments)
            {
                if (ReferenceEquals(edge, incoming) || edge is not IReadOnlySegment<TPosition, TRotation> segment
                    || segment.IsDisposed || segment.Length <= 0f) continue;
                var outgoing = ReferenceEquals(segment.From, node);
                if ((!outgoing && !ReferenceEquals(segment.To, node)) || segment.From == null || segment.To == null)
                    continue;
                var direction = outgoing ? TraversalOptions.Forward : TraversalOptions.Backward;
                if ((segment.TraversalOptions & direction) != 0
                    && segment.TryGetPosition(new Location(0f, isForward: outgoing), out _))
                    candidates.Add((segment, outgoing));
            }

            if (candidates.Count == 0) return false;
            int index = 0;
            if (candidates.Count > 1)
            {
                bool reserved = ReservedRoutes.Count > 0;
                int requested = reserved ? ReservedRoutes.Peek() : -1;
                if ((uint)requested < (uint)candidates.Count) index = requested;
                else if (incoming.TryGetTransform(new Location(incoming.Length, isForward: forward),
                    out _, out var arrival))
                {
                    var best = float.PositiveInfinity;
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var candidate = candidates[i];
                        if (!candidate.Segment.TryGetTransform(new Location(0f, isForward: candidate.Forward),
                            out _, out var departure)) continue;
                        var angle = turnAngle != null ? turnAngle(arrival, departure)
                            : PlanarTurnAngle(incoming, arrival, departure);
                        if (!(angle >= 0f && angle < best)) continue;
                        best = angle;
                        index = i;
                    }
                }
            }

            var (next, nextForward) = candidates[index];
            if (!enter(next, nextForward)) return false;
            if (candidates.Count > 1 && ReservedRoutes.Count > 0) ReservedRoutes.Dequeue();
            return true;
        }

        private float PlanarTurnAngle<TRotation>(IReadOnlySegment segment, TRotation arrival, TRotation departure)
        {
            if (segment is not Geometry.Planar.IReadOnlySegment
                || arrival is not float from || departure is not float to)
                return float.NaN;
            return (float)Math.Abs(Math.IEEERemainder((double)to - from, 360d));
        }
    }
}
