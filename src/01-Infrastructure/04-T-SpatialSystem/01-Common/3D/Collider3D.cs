using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class Collider3D : SpatialHandle, ICollisionComponent
    {
        // Front
        public CollisionSpace3D Space { get; }
        SpatialSpace ICollisionComponent.Space => Space;
        public Transform3D Pose
        {
            get { ThrowIfInvalid(); return ReadBinding(_bindings.Pose.Read); }
            set
            {
                ThrowIfInvalid();
                SpatialValidation.Pose(value, nameof(value));
                WriteBinding(() => _bindings.Pose.Write(value));
            }
        }
        public bool Enabled
        {
            get { ThrowIfInvalid(); return _pendingEnabled ?? ReadBinding(_bindings.Enabled.Read); }
            set
            {
                ThrowIfInvalid();
                _pendingEnabled = value;
                _enabledRequest++;
            }
        }
        public bool IsTrigger
        {
            get { ThrowIfInvalid(); return _pendingTrigger ?? ReadBinding(_bindings.IsTrigger.Read); }
            set
            {
                ThrowIfInvalid();
                _pendingTrigger = value;
                _triggerRequest++;
            }
        }
        public int Layer
        {
            get { ThrowIfInvalid(); return ReadBinding(_bindings.Layer.Read); }
            set
            {
                ThrowIfInvalid();

                if (value < 0 || value > 31) throw new ArgumentOutOfRangeException(nameof(value));

                WriteBinding(() => _bindings.Layer.Write(value));
            }
        }

        // Control
        public event Action<CollisionPair3D> CollisionEnter
        {
            add { ThrowIfInvalid(); _collisionEnter += value; }
            remove => _collisionEnter -= value;
        }
        public event Action<CollisionPair3D> CollisionExit
        {
            add { ThrowIfInvalid(); _collisionExit += value; }
            remove => _collisionExit -= value;
        }
        public event Action<CollisionPair3D> TriggerEnter
        {
            add { ThrowIfInvalid(); _triggerEnter += value; }
            remove => _triggerEnter -= value;
        }
        public event Action<CollisionPair3D> TriggerExit
        {
            add { ThrowIfInvalid(); _triggerExit += value; }
            remove => _triggerExit -= value;
        }

        // Internal
        // The colliders whose start was delivered and whose end was not, kept on both sides.
        internal Dictionary<(Collider3D Other, bool Trigger), CollisionNotification3D> Relations { get; } = new();
        internal bool Active { get; private set; } = true;
        internal bool AppliedTrigger { get; private set; }
        internal long ActiveRevision { get; private set; }
        internal long TriggerRevision { get; private set; }
        private bool _prepared;
        private bool? _pendingEnabled;
        private bool? _pendingTrigger;
        private bool? _nextEnabled;
        private bool? _nextTrigger;
        private long _enabledRequest;
        private long _triggerRequest;
        private long _frozenEnabledRequest;
        private long _frozenTriggerRequest;
        private ColliderBindings3D _bindings;
        private Action<CollisionPair3D> _collisionEnter;
        private Action<CollisionPair3D> _collisionExit;
        private Action<CollisionPair3D> _triggerEnter;
        private Action<CollisionPair3D> _triggerExit;


        // Content
        public Collider3D(CollisionSpace3D space, ColliderBindings3D bindings)
            : base(CreateValidity(space, bindings), bindings.Release)
        {
            Space = space;
            _bindings = bindings;
            Space.Register(this);
        }

        private static Func<bool> CreateValidity(CollisionSpace3D space, ColliderBindings3D bindings)
        {
            SpatialValidation.Required(space, nameof(space));
            SpatialValidation.Required(bindings, nameof(bindings));

            if (!space.IsValid) throw new ObjectDisposedException(nameof(space));

            return bindings.IsValid;
        }

        protected override bool OwnerValid => Space.IsValid;

        internal void FreezeSettings()
        {
            _nextEnabled = _pendingEnabled;
            _nextTrigger = _pendingTrigger;
            _frozenEnabledRequest = _enabledRequest;
            _frozenTriggerRequest = _triggerRequest;
        }

        internal void ApplySettings()
        {
            TryRunBinding(() =>
            {
                if (_nextEnabled.HasValue)
                {
                    _bindings.Enabled.Write(_nextEnabled.Value);
                    if (_enabledRequest == _frozenEnabledRequest) _pendingEnabled = null;
                }
                if (_nextTrigger.HasValue)
                {
                    _bindings.IsTrigger.Write(_nextTrigger.Value);
                    if (_triggerRequest == _frozenTriggerRequest) _pendingTrigger = null;
                }
            });
        }

        internal void Prepare()
        {
            TryRunBinding(() =>
            {
                _bindings.Prepare?.Invoke();
                if (IsDisposed) return;
                var active = _bindings.Enabled.Read() && (_bindings.IsActive?.Invoke() ?? true);
                var trigger = _bindings.IsTrigger.Read();

                // The first observation only learns how the object was created.
                if (_prepared && active != Active) ActiveRevision++;
                if (_prepared && trigger != AppliedTrigger) TriggerRevision++;
                _prepared = true;
                Active = active;
                AppliedTrigger = trigger;
            });
        }

        internal void Publish(CollisionNotification3D notification)
        {
            var handlers = notification.IsTrigger
                ? (notification.IsEntering ? _triggerEnter : _triggerExit)
                : (notification.IsEntering ? _collisionEnter : _collisionExit);
            SpatialDispatch.Invoke(handlers, notification.Pair,
                () => IsValid && notification.Pair.First.IsValid && notification.Pair.Second.IsValid,
                (_, exception) => notification.Pair.First.Fail(exception, notification.Pair.Second));
        }

        protected override void OnDispose()
        {
            _bindings = null;
            _collisionEnter = null;
            _collisionExit = null;
            _triggerEnter = null;
            _triggerExit = null;
            Space.Unregister(this);
        }
    }
}
