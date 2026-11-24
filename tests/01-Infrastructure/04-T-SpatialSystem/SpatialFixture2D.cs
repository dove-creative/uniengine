using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Objects;
using BlackThunder.UniEngine.SpatialSystem;

namespace BlackThunder.UniEngine.Tests
{
    internal class SpatialFixture2D : IDisposable
    {
        internal class Cell<T>
        {
            public T Value;
            public SpatialProperty<T> Property => new(() => Value, value => Value = value);
            public Cell(T value) => Value = value;
        }

        internal class ColliderState
        {
            public bool Alive = true;
            public int Releases;
            public bool Active = true;
            public Func<bool> Check;
            public Action Prepare;
            public Cell<Transform2D> Pose = new(new(Vector2.Zero, 0f));
            public Cell<bool> Enabled = new(true);
            public Cell<bool> Trigger = new(false);
            public Cell<int> Layer = new(0);
        }

        public bool Alive = true;
        public int Releases;
        public int Disconnects;
        public CollisionSpace2D Space;
        public Collider2D First;
        public Collider2D Second;
        public ColliderState FirstState = new();
        public ColliderState SecondState = new();
        public List<SpatialRaycastHit2D> Hits = new();
        public List<Collider2D> Overlaps = new();
        public Action<CollisionNotification2D> Notify;
        public Vector2 LastDirection;
        public SpatialQueryFilter LastFilter;
        public int QueryCount;
        public int UpdateCount;
        public float ElapsedTime;
        public Action<float> OnUpdate;
        private long _nextRelationship;
        private readonly Dictionary<bool, long> _nativeRelationships = new();
        private readonly Dictionary<bool, long> _lastRelationships = new();
        private readonly bool[,] _layers = new bool[32, 32];


        public SpatialFixture2D()
        {
            for (var first = 0; first < 32; first++)
                for (var second = 0; second < 32; second++) _layers[first, second] = true;
            Space = new CollisionSpace2D(new CollisionSpaceBindings2D(
                () => Alive,
                (origin, direction, distance, filter) =>
                {
                    QueryCount++;
                    LastDirection = direction;
                    LastFilter = filter;
                    return Hits;
                },
                (pose, size, filter) => Capture(filter),
                (center, radius, filter) => Capture(filter),
                (pose, size, direction, filter) => Capture(filter),
                notify => { Notify = notify; return () => Disconnects++; },
                new CollisionWorldBindings2D(
                    deltaTime => { UpdateCount++; ElapsedTime += deltaTime; OnUpdate?.Invoke(deltaTime); },
                    (first, second) => _layers[first, second],
                    (first, second, value) => { _layers[first, second] = _layers[second, first] = value; },
                    CreateBox, CreateCircle, CreateCapsule),
                () => Releases++));
            First = new Collider2D(Space, Bindings(FirstState));
            Second = new Collider2D(Space, Bindings(SecondState));
        }

        public ColliderBindings2D Bindings(ColliderState state) => new(
            state.Pose.Property, state.Enabled.Property, state.Trigger.Property, state.Layer.Property,
            () => state.Check?.Invoke() ?? state.Alive, () => state.Releases++, () => state.Active,
            () =>
            {
                state.Prepare?.Invoke();
                if (!state.Enabled.Value || !state.Active) _nativeRelationships.Clear();
                else _nativeRelationships.Remove(!(FirstState.Trigger.Value || SecondState.Trigger.Value));
            });

        private ColliderBindings2D CreatedBindings(Transform2D pose)
        {
            var state = new ColliderState();
            state.Pose.Value = pose;
            return new ColliderBindings2D(
                state.Pose.Property, state.Enabled.Property, state.Trigger.Property, state.Layer.Property,
                () => state.Alive, () => state.Releases++);
        }

        private BoxCollider2D CreateBox(Transform2D pose, Vector2 size) => new(
            Space, CreatedBindings(pose), new Cell<Vector2>(size).Property, new Cell<Vector2>(Vector2.Zero).Property);

        private CircleCollider2D CreateCircle(Transform2D pose, float radius) => new(
            Space, CreatedBindings(pose), new Cell<float>(radius).Property, new Cell<Vector2>(Vector2.Zero).Property);

        private CapsuleCollider2D CreateCapsule(Transform2D pose, Vector2 size, CapsuleDirection2D direction) => new(
            Space, CreatedBindings(pose), new Cell<Vector2>(size).Property,
            new Cell<Vector2>(Vector2.Zero).Property, new Cell<CapsuleDirection2D>(direction).Property);

        private IReadOnlyList<Collider2D> Capture(SpatialQueryFilter filter)
        {
            QueryCount++;
            LastFilter = filter;
            return Overlaps;
        }

        public void Send(bool entering, bool trigger = false, bool reverse = false)
        {
            if (entering && !_nativeRelationships.ContainsKey(trigger))
            {
                _nativeRelationships[trigger] = ++_nextRelationship;
                _lastRelationships[trigger] = _nextRelationship;
            }
            if (!_lastRelationships.TryGetValue(trigger, out var id)) id = ++_nextRelationship;
            if (!entering) _nativeRelationships.Remove(trigger);
            Notify(new CollisionNotification2D(
                reverse ? new CollisionPair2D(
                    Second,
                    First) : new CollisionPair2D(First, Second), trigger, entering, id));
        }

        public void Dispose() => Space.Dispose();
    }
}
