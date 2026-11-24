#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Objects;
using N = System.Numerics;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    internal class UnityReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public static UnityReferenceComparer<T> Instance { get; } = new();

        public bool Equals(T first, T second) => ReferenceEquals(first, second);
        public int GetHashCode(T value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }

    internal class UnityObjectReference
    {
        public U.Object Value { get; private set; }
        public int Id { get; }

        // The relations reported as started, by the other collider and kind, with their IDs.
        public Dictionary<(UnityObjectReference Other, bool Trigger), long> Relations { get; } = new();
        private readonly List<Action> _connections = new();


        public UnityObjectReference(U.Object value)
        {
            Value = value;
            Id = value.GetInstanceID();
        }

        public void Connect(Action disconnect) => _connections.Add(disconnect);

        public void StartRelation(UnityObjectReference other, bool trigger, long id)
        {
            Relations.Add((other, trigger), id);
            other.Relations.Add((this, trigger), id);
        }

        public void EndRelation(UnityObjectReference other, bool trigger)
        {
            Relations.Remove((other, trigger));
            other.Relations.Remove((this, trigger));
        }

        public void EndRelations()
        {
            foreach (var (other, trigger) in Relations.Keys) other.Relations.Remove((this, trigger));
            Relations.Clear();
        }

        public void Release()
        {
            var errors = new List<Exception>();

            for (var index = _connections.Count - 1; index >= 0; index--)
            {
                try { _connections[index](); }
                catch (Exception exception) { errors.Add(exception); }
            }

            _connections.Clear();
            Value = null;

            if (errors.Count != 0) throw new AggregateException(errors);
        }
    }

    internal static class UnityBindingUtility
    {
        public static T Engine<T>(Func<T> action)
        {
            try { return action(); }
            catch (U.UnityException exception)
            {
                throw new SpatialEngineException("Unity could not complete the operation.", exception);
            }
        }

        public static void Engine(Action action) => Engine(() => { action(); return true; });

        public static SpatialProperty<T> Property<T>(Func<T> read, Action<T> write) =>
            new(() => Engine(read), value => Engine(() => write(value)));

        public static U.Vector2 Native(N.Vector2 value) => new(value.X, value.Y);
        public static U.Vector3 Native(N.Vector3 value) => new(value.X, value.Y, value.Z);
        public static U.Quaternion Native(N.Quaternion value) => new(value.X, value.Y, value.Z, value.W);
        public static N.Vector2 Common(U.Vector2 value) => new(value.x, value.y);
        public static N.Vector3 Common(U.Vector3 value) => new(value.x, value.y, value.z);
        public static N.Quaternion Common(U.Quaternion value) => new(value.x, value.y, value.z, value.w);

        public static Transform2D Pose2D(U.Transform transform) =>
            new(new N.Vector2(transform.position.x, transform.position.y), transform.eulerAngles.z);

        public static Transform3D Pose3D(U.Transform transform) =>
            new(Common(transform.position), Common(transform.rotation));

        public static void SetPose2D(U.Transform transform, Transform2D pose) =>
            transform.SetPositionAndRotation(
                new U.Vector3(pose.Position.X, pose.Position.Y, transform.position.z),
                U.Quaternion.Euler(0f, 0f, pose.Rotation));

        public static void SetPose3D(U.Transform transform, Transform3D pose) =>
            transform.SetPositionAndRotation(Native(pose.Position), Native(pose.Rotation));

        public static U.ContactFilter2D Filter(SpatialQueryFilter filter)
        {
            var result = new U.ContactFilter2D();
            result.SetLayerMask(filter.LayerMask);
            result.useTriggers = filter.IncludeTriggers;
            return result;
        }

        public static U.QueryTriggerInteraction TriggerFilter(SpatialQueryFilter filter) =>
            filter.IncludeTriggers ? U.QueryTriggerInteraction.Collide : U.QueryTriggerInteraction.Ignore;

        public static U.CapsuleDirection2D Native(CapsuleDirection2D direction) =>
            direction == CapsuleDirection2D.Horizontal
                ? U.CapsuleDirection2D.Horizontal : U.CapsuleDirection2D.Vertical;

        public static CapsuleDirection2D Common(U.CapsuleDirection2D direction) =>
            direction == U.CapsuleDirection2D.Horizontal ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;

        public static T[] QueryAll<T>(Func<T[], int> query)
        {
            var capacity = 16;

            while (true)
            {
                var buffer = new T[capacity];
                var count = Engine(() => query(buffer));

                if (count < 0 || count > capacity)
                    throw new InvalidOperationException("Unity returned an invalid query result count.");

                if (count < capacity)
                {
                    Array.Resize(ref buffer, count);
                    return buffer;
                }

                capacity = checked(capacity * 2);
            }
        }

        public static void DestroyOwned(U.Object value)
        {
            if (value) U.Object.Destroy(value);
        }

        public static void RunAll(IEnumerable<Action> actions)
        {
            var errors = new List<Exception>();

            foreach (var action in actions)
            {
                try { action(); }
                catch (Exception exception) { errors.Add(exception); }
            }

            if (errors.Count != 0) throw new AggregateException(errors);
        }
    }
}
#endif
