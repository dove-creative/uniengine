using System;
using System.Collections.Generic;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.UniEngine.Mathematics;

namespace BlackThunder.GeoSystem.RouteSystem
{
    internal class TerminalSpan
    {
        public IReadOnlySegment Segment { get; }
        public double Start { get; }
        public double End { get; }
        public double SegmentStart { get; }
        public double SegmentEnd { get; }
        public bool IsForward { get; }
        private readonly ISegmentView[] _views;


        public TerminalSpan(IReadOnlySegment segment, double start, double end, double segmentStart,
            double segmentEnd, bool isForward, List<ISegmentView> views)
        {
            Segment = segment;
            Start = start;
            End = end;
            SegmentStart = segmentStart;
            SegmentEnd = segmentEnd;
            IsForward = isForward;
            _views = views.ToArray();
        }

        public bool SameOccurrence(TerminalSpan other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (!ReferenceEquals(Segment, other.Segment) || _views.Length != other._views.Length) return false;
            for (int i = 0; i < _views.Length; i++)
                if (!ReferenceEquals(_views[i], other._views[i])) return false;
            return true;
        }

        public Location ToTerminal(double routeDistance, bool forward)
        {
            var offset = routeDistance - Start;
            var absolute = routeDistance <= Start ? SegmentStart : routeDistance >= End ? SegmentEnd
                : Math.Clamp(SegmentStart + (IsForward ? offset : -offset),
                    Math.Min(SegmentStart, SegmentEnd), Math.Max(SegmentStart, SegmentEnd));
            var direction = forward == IsForward;
            return new Location((float)(direction ? absolute : Segment.Length - absolute), isForward: direction);
        }

        public bool TryToRoute(Location location, float epsilon, out Location routeLocation, float length)
        {
            routeLocation = default;
            if (!location.TryResolve(Segment.Length, epsilon, out var absolute)) return false;
            var offset = IsForward ? absolute - SegmentStart : SegmentStart - absolute;
            var distance = absolute == SegmentEnd ? End : Start + offset;
            if (distance < Start - epsilon || distance > End + epsilon) return false;
            distance = Math.Clamp(distance, Start, End);
            var forward = location.IsForward == IsForward;
            routeLocation = new Location((float)(forward ? distance : length - distance), isForward: forward);
            return true;
        }
    }

    internal class TerminalPath
    {
        private readonly List<TerminalSpan> _spans = new();
        private readonly IReadOnlySegment _source;
        public TerminalSpan this[int index] => _spans[index];
        public float Epsilon => _source switch
        {
            Route route => route.TerminalEpsilon,
            Planar.Clip clip => clip.Epsilon,
            _ => UniMath.Epsilon
        };


        public TerminalPath(IReadOnlySegment source)
        {
            _source = source;
            Add(source, 0, source.Length, true, 0, source.Length, new List<ISegmentView>());
        }

        public static TerminalPath For(IReadOnlySegment source)
        {
            if (source.IsDisposed) throw new ObjectDisposedException(nameof(source));
            return source is Route builtIn ? builtIn.TerminalPath : new TerminalPath(source);
        }

        // Clip limits are expressed in the source's From-based coordinates.
        private void Add(IReadOnlySegment segment, double low, double high, bool forward, double start,
            double end, List<ISegmentView> views)
        {
            if (segment.IsDisposed || segment.Length <= 0f) return;
            if (segment is IClip clip)
            {
                if (!clip.IsValid || clip.SegmentSource == null) return;
                var source = clip.SegmentSource;
                var origin = Math.Clamp(clip.From.Location.ToDistance(source.Length), 0f, source.Length);
                var destination = Math.Clamp(clip.To.Location.ToDistance(source.Length), 0f, source.Length);
                double Map(double distance) => distance == clip.Length ? destination
                    : Math.Clamp(origin + (clip.IsForward ? distance : -distance), 0, source.Length);
                Add(source, Map(clip.IsForward ? low : high), Map(clip.IsForward ? high : low),
                    forward == clip.IsForward, start, end, views);
                return;
            }

            if (segment is IRoute route)
            {
                // Public Length is a float; a full view must still reach the exact endpoint of its final child.
                double sum = 0;
                foreach (var view in route.Views) sum += view.Source.Length;
                if (low == route.Length) low = sum;
                if (high == route.Length) high = sum;
                double boundary = forward ? 0 : sum;
                for (int i = 0; i < route.Views.Count; i++)
                {
                    var view = route.Views[forward ? i : route.Views.Count - 1 - i];
                    var length = view.Source.Length;
                    var from = forward ? boundary : boundary - length;
                    boundary += forward ? length : -length;
                    var left = Math.Max(low, from);
                    var right = Math.Min(high, from + length);
                    if (right < left || length <= 0f) continue;
                    // A clipped join can select either adjacent segment at the exact endpoint.
                    var sourceLow = view.IsForward ? left - from : length - (right - from);
                    var sourceHigh = view.IsForward ? right - from : length - (left - from);
                    var first = forward ? left - low : high - right;
                    var last = forward ? right - low : high - left;
                    views.Add(view);
                    Add(view.Source, sourceLow, sourceHigh, forward == view.IsForward,
                        first == high - low ? end : Math.Min(start + first, end),
                        last == high - low ? end : Math.Min(start + last, end), views);
                    views.RemoveAt(views.Count - 1);
                }
                return;
            }

            _spans.Add(new TerminalSpan(segment, start, end, forward ? low : high, forward ? high : low, forward, views));
        }

        public int FindOccurrence(TerminalSpan occurrence, int hint)
        {
            if ((uint)hint < (uint)_spans.Count && _spans[hint].SameOccurrence(occurrence)) return hint;
            return _spans.FindIndex(span => span.SameOccurrence(occurrence));
        }

        public bool TryResolve(Location location, int hint, out IReadOnlySegment segment,
            out Location terminalLocation, out int index, float? epsilon = null)
        {
            var tolerance = epsilon ?? Epsilon;
            Location.ValidateEpsilon(tolerance);
            segment = null;
            terminalLocation = default;
            index = -1;
            if (_source.Length <= tolerance || !location.TryResolve(_source.Length, tolerance, out var distance)
                || _spans.Count == 0) return false;

            var forward = location.IsForward;
            bool BeforeBoundary(int i) => forward
                ? _spans[i].End + tolerance < distance
                : _spans[i].Start - tolerance <= distance;

            // Both search paths use the same directional tie-break at a shared boundary.
            var split = forward ? hint : (long)hint + 1;
            if ((uint)hint >= (uint)_spans.Count || (split > 0 && !BeforeBoundary((int)split - 1))
                || (split < _spans.Count && BeforeBoundary((int)split)))
            {
                int low = 0, high = _spans.Count;
                while (low < high)
                {
                    var middle = low + (high - low) / 2;
                    if (BeforeBoundary(middle)) low = middle + 1;
                    else high = middle;
                }
                split = low;
            }

            var selected = (int)(forward ? split : split - 1);
            if ((uint)selected >= (uint)_spans.Count) return false;
            var span = _spans[selected];
            if (distance < span.Start - tolerance || distance > span.End + tolerance) return false;
            index = selected;
            segment = span.Segment;
            terminalLocation = span.ToTerminal(distance, forward);
            return true;
        }
    }
}
