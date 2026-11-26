using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.GeoSystem.Navigation
{
    internal class GroupReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public bool Equals(T x, T y) => ReferenceEquals(x, y);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }

    internal class GroupState : IDisposable
    {
        public LifecycleHub Hub { get; } = new();
        public bool IsDisposed => _disposeRequested || Hub.IsDisposed;
        private readonly List<Action> _clearers = new();
        private bool _disposeRequested;


        public void Register(Action clear) => _clearers.Add(clear);

        public void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException("Group");
        }

        public void Clear()
        {
            ThrowIfDisposed();
            ClearContents();
        }

        private void ClearContents()
        {
            List<Exception> errors = null;
            foreach (var clear in _clearers)
            {
                try { clear(); }
                catch (Exception error) { (errors ??= new()).Add(error); }
            }
            if (errors != null) throw new AggregateException(errors);
        }

        public void Dispose()
        {
            if (_disposeRequested) return;
            _disposeRequested = true;
            try { ClearContents(); }
            finally { Hub.Dispose(); }
        }
    }

    internal class GroupMembers<T> where T : class, ILifecycleSubject
    {
        public IReadOnlyList<T> Items { get; }
        private readonly GroupState _state;
        private readonly List<T> _items = new();
        private readonly HashSet<T> _members = new(new GroupReferenceComparer<T>());


        public GroupMembers(GroupState state)
        {
            _state = state;
            Items = _items.AsReadOnly();
            state.Register(Clear);
        }

        public bool Add(T item)
        {
            _state.ThrowIfDisposed();
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.IsDisposed) throw new ArgumentException("The member is disposed.", nameof(item));
            if (_members.Contains(item)) return false;

            try { item.LifecycleHub.Connect(this).OnDisposed(() => RemoveCore(item)); }
            catch
            {
                item.LifecycleHub.Disconnect(this);
                throw;
            }
            _items.Add(item);
            _members.Add(item);
            return true;
        }

        public bool Remove(T item)
        {
            _state.ThrowIfDisposed();
            return RemoveCore(item);
        }

        private bool RemoveCore(T item)
        {
            if (item == null || !_members.Remove(item)) return false;
            _items.RemoveAt(_items.FindIndex(candidate => ReferenceEquals(candidate, item)));
            item.LifecycleHub.Disconnect(this);
            return true;
        }

        private void Clear()
        {
            var removed = _items.ToArray();
            _items.Clear();
            _members.Clear();
            List<Exception> errors = null;
            foreach (var item in removed)
            {
                try { item.LifecycleHub.Disconnect(this); }
                catch (Exception error) { (errors ??= new()).Add(error); }
            }
            if (errors != null) throw new AggregateException(errors);
        }
    }

    internal static class GroupHierarchy
    {
        public static void Validate(IReadOnlyNavGroup owner, IReadOnlyNavGroup group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (group.IsDisposed) throw new ArgumentException("The group is disposed.", nameof(group));
            var active = new HashSet<IReadOnlyNavGroup>(new GroupReferenceComparer<IReadOnlyNavGroup>());
            var visited = new HashSet<IReadOnlyNavGroup>(new GroupReferenceComparer<IReadOnlyNavGroup>());
            var pending = new Stack<(IReadOnlyNavGroup Group, bool Leaving)>();
            pending.Push((group, false));
            while (pending.Count != 0)
            {
                var (current, leaving) = pending.Pop();
                if (leaving)
                {
                    active.Remove(current);
                    continue;
                }
                if (ReferenceEquals(current, owner) || active.Contains(current))
                    throw new ArgumentException("Group nesting cannot contain a cycle.", nameof(group));
                if (!visited.Add(current)) continue;
                active.Add(current);
                pending.Push((current, true));
                foreach (var child in current.NestedGroups) pending.Push((child, false));
            }
        }

        public static IEnumerable<TGroup> Traverse<TGroup>(
            TGroup root, Func<TGroup, IReadOnlyList<TGroup>> children) where TGroup : class, ILifecycleSubject
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var visited = new HashSet<TGroup>(new GroupReferenceComparer<TGroup>());
            var pending = new Stack<TGroup>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                var group = pending.Pop();
                if (!visited.Add(group)) continue;
                yield return group;
                var nested = children(group);
                for (int i = nested.Count - 1; i >= 0; i--) pending.Push(nested[i]);
            }
        }

        public static IEnumerable<TMember> Members<TGroup, TMember>(
            TGroup root,
            Func<TGroup, IReadOnlyList<TGroup>> children,
            Func<TGroup, IReadOnlyList<TMember>> members)
            where TGroup : class, ILifecycleSubject
            where TMember : class
        {
            var visited = new HashSet<TMember>(new GroupReferenceComparer<TMember>());
            foreach (var group in Traverse(root, children))
                foreach (var member in members(group))
                    if (visited.Add(member)) yield return member;
        }
    }
}
