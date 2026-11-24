using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Objects;
using BlackThunder.UniEngine.SpatialSystem;

namespace BlackThunder.UniEngine.Tests
{
    internal class SpatialFixture3D : IDisposable
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
            public Cell<Transform3D> Pose = new(new(Vector3.Zero, Quaternion.Identity));
            public Cell<bool> Enabled = new(true);
            public Cell<bool> Trigger = new(false);
            public Cell<int> Layer = new(0);
        }

        public bool Alive = true;
        public int Releases;
        public int Disconnects;
        public CollisionSpace3D Space;
        public Collider3D First;
        public Collider3D Second;
        public ColliderState FirstState = new();
        public ColliderState SecondState = new();
        public List<SpatialRaycastHit3D> Hits = new();
        public List<Collider3D> Overlaps = new();
        public Action<CollisionNotification3D> Notify;
        public Vector3 LastDirection;
        public SpatialQueryFilter LastFilter;
        public int QueryCount;
        public int UpdateCount;
        public float ElapsedTime;
        public Action<float> OnUpdate;
        private long _nextRelationship;
        private readonly Dictionary<bool, long> _nativeRelationships = new();
        private readonly Dictionary<bool, long> _lastRelationships = new();
        private readonly bool[,] _layers = new bool[32, 32];


        public SpatialFixture3D()
        {
            for (var first = 0; first < 32; first++)
                for (var second = 0; second < 32; second++) _layers[first, second] = true;
            Space = new CollisionSpace3D(new CollisionSpaceBindings3D(
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
                (first, second, radius, filter) => Capture(filter),
                notify => { Notify = notify; return () => Disconnects++; },
                new CollisionWorldBindings3D(
                    deltaTime => { UpdateCount++; ElapsedTime += deltaTime; OnUpdate?.Invoke(deltaTime); },
                    (first, second) => _layers[first, second],
                    (first, second, value) => { _layers[first, second] = _layers[second, first] = value; },
                    CreateBox, CreateSphere, CreateCapsule),
                () => Releases++));
            First = new Collider3D(Space, Bindings(FirstState));
            Second = new Collider3D(Space, Bindings(SecondState));
        }

        public ColliderBindings3D Bindings(ColliderState state) => new(
            state.Pose.Property, state.Enabled.Property, state.Trigger.Property, state.Layer.Property,
            () => state.Check?.Invoke() ?? state.Alive, () => state.Releases++, () => state.Active,
            () =>
            {
                state.Prepare?.Invoke();
                if (!state.Enabled.Value || !state.Active) _nativeRelationships.Clear();
                else _nativeRelationships.Remove(!(FirstState.Trigger.Value || SecondState.Trigger.Value));
            });

        private ColliderBindings3D CreatedBindings(Transform3D pose)
        {
            var state = new ColliderState();
            state.Pose.Value = pose;
            return new ColliderBindings3D(
                state.Pose.Property, state.Enabled.Property, state.Trigger.Property, state.Layer.Property,
                () => state.Alive, () => state.Releases++);
        }

        private BoxCollider3D CreateBox(Transform3D pose, Vector3 size) => new(
            Space, CreatedBindings(pose), new Cell<Vector3>(size).Property, new Cell<Vector3>(Vector3.Zero).Property);

        private SphereCollider3D CreateSphere(Transform3D pose, float radius) => new(
            Space, CreatedBindings(pose), new Cell<float>(radius).Property, new Cell<Vector3>(Vector3.Zero).Property);

        private CapsuleCollider3D CreateCapsule(
            Transform3D pose,
            float radius,
            float height,
            CapsuleDirection3D direction) => new(
            Space, CreatedBindings(pose), new Cell<float>(radius).Property, new Cell<float>(height).Property,
            new Cell<Vector3>(Vector3.Zero).Property, new Cell<CapsuleDirection3D>(direction).Property);

        private IReadOnlyList<Collider3D> Capture(SpatialQueryFilter filter)
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
            Notify(new CollisionNotification3D(
                reverse ? new CollisionPair3D(
                    Second,
                    First) : new CollisionPair3D(First, Second), trigger, entering, id));
        }

        public void Dispose() => Space.Dispose();
    }
}
