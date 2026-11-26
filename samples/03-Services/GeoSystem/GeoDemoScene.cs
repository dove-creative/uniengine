using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Core;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Planar;
using BlackThunder.UniEngine.Objects;
using BlackThunder.GeoSystem.RouteSystem;
using BlackThunder.GeoSystem.RouteSystem.Planar;
using SegmentView = BlackThunder.GeoSystem.RouteSystem.Planar.SegmentView;
using Route = BlackThunder.GeoSystem.RouteSystem.Planar.Route;

namespace BlackThunder.UniEngine.Samples.GeoSystem
{
    // This scenario owns the engine objects and has no UI framework dependency.
    public sealed class GeoDemoScene : IDisposable
    {
        // Front
        public IReadOnlyList<Node> Nodes { get; private set; } = Array.Empty<Node>();
        public IReadOnlyList<LinearSegment> Segments { get; private set; } = Array.Empty<LinearSegment>();
        public SegmentPin ClipStart { get; private set; }
        public SegmentPin ClipEnd { get; private set; }
        public Clip Clip { get; private set; }
        public Route Route { get; private set; }
        public SegmentPin Traveller { get; private set; }
        public Hold Hold { get; private set; }
        public IReadOnlyList<string> Events { get; }
        public bool IsPlaying { get; private set; }
        public bool IsForward { get; private set; } = true;
        public float Progress { get; private set; } = 0.35f;
        public float Speed { get; private set; } = 2f;
        public bool IsDisposed { get; private set; }
        public event Action Changed;

        // Internal
        private readonly List<string> _events = new();
        private bool _suppressEvents;
        private int _eventNumber;


        // Content
        public GeoDemoScene()
        {
            Events = _events.AsReadOnly();
            Reset();
        }

        public void Reset()
        {
            ThrowIfDisposed();
            _suppressEvents = true;
            ReleaseGraph();
            var nodes = new[]
            {
                new Node(new Vector2(-6f, -1.5f)),
                new Node(new Vector2(-2f, 2f)),
                new Node(new Vector2(3f, 0.5f)),
                new Node(new Vector2(6f, -2f))
            };
            Nodes = Array.AsReadOnly(nodes);
            var segments = new[]
            {
                new LinearSegment(nodes[0], nodes[1]),
                new LinearSegment(nodes[1], nodes[2]),
                new LinearSegment(nodes[2], nodes[3])
            };
            Segments = Array.AsReadOnly(segments);
            ClipStart = new SegmentPin(segments[1], new Location(0.2f, DistanceType.Rate));
            ClipEnd = new SegmentPin(segments[1], new Location(0.8f, DistanceType.Rate));
            Clip = new Clip(ClipStart, ClipEnd);
            Route = new Route();
            foreach (var segment in segments) Route.Append(new SegmentView(segment));
            IsForward = true;
            Progress = 0.35f;
            Speed = 2f;
            IsPlaying = false;
            Traveller = new SegmentPin(Route, new Location(Progress, DistanceType.Rate));
            Hold = new Hold();
            Hold.MoveTo(Traveller);

            for (var i = 0; i < nodes.Length; i++) Observe(nodes[i], ((char)('A' + i)).ToString());
            for (var i = 0; i < segments.Length; i++) Observe(segments[i], $"Segment {i + 1}");
            Observe(ClipStart, "Clip.From");
            Observe(ClipEnd, "Clip.To");
            Observe(Clip, "Clip");
            Observe(Route, "Route");
            _events.Clear();
            _eventNumber = 0;
            _suppressEvents = false;
            Record("Scene", "Reset");
            Changed?.Invoke();
        }

        private void Observe(IEventSubject<int> subject, string name) =>
            subject.EventHub.Connect(this).OnUpdated(flags => Record(name, ((RouteEvents)flags).ToString()));

        private void Record(string name, string events)
        {
            if (_suppressEvents) return;
            _events.Insert(0, $"{++_eventNumber:000}  {name}  ·  {events}");
            if (_events.Count > 12) _events.RemoveAt(_events.Count - 1);
        }

        public void MoveNode(int index, Vector2 position)
        {
            ThrowIfDisposed();
            if (index < 0 || index >= Nodes.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Nodes[index].Position = position;
            // Publish after the synchronous engine update has reached every dependent object.
            Changed?.Invoke();
        }

        public void SetClipRate(bool start, float rate)
        {
            ThrowIfDisposed();
            ValidateRate(rate);
            (start ? ClipStart : ClipEnd).Location = new Location(rate, DistanceType.Rate);
            Changed?.Invoke();
        }

        public void SetProgress(float progress)
        {
            ThrowIfDisposed();
            ValidateRate(progress);
            Progress = progress;
            Traveller.Location = new Location(progress, DistanceType.Rate, IsForward);
            Changed?.Invoke();
        }

        public void SetForward(bool forward)
        {
            ThrowIfDisposed();
            IsForward = forward;
            SetProgress(Progress);
        }

        public void SetSpeed(float speed)
        {
            ThrowIfDisposed();
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
            Changed?.Invoke();
        }

        public void SetPlaying(bool playing)
        {
            ThrowIfDisposed();
            IsPlaying = playing;
            Changed?.Invoke();
        }

        public void Advance(float seconds)
        {
            ThrowIfDisposed();
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!IsPlaying || seconds == 0f || Route.Length <= Route.Epsilon) return;
            SetProgress((float)((Progress + (double)seconds * Speed / Route.Length) % 1d));
        }

        public bool TryGetPose(out Transform2D transform)
        {
            ThrowIfDisposed();
            return Route.TryGetTransform(new Location(Progress, DistanceType.Rate, IsForward), out transform);
        }

        private void ValidateRate(float rate)
        {
            if (float.IsNaN(rate) || rate < 0f || rate > 1f) throw new ArgumentOutOfRangeException(nameof(rate));
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(GeoDemoScene));
        }

        private void ReleaseGraph()
        {
            Hold?.Dispose();
            Traveller?.Dispose();
            Route?.Dispose();
            Clip?.Dispose();
            ClipStart?.Dispose();
            ClipEnd?.Dispose();
            foreach (var segment in Segments) segment.Dispose();
            foreach (var node in Nodes) node.Dispose();
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _suppressEvents = true;
            Changed = null;
            ReleaseGraph();
        }
    }
}
