using System;
using System.Collections.Generic;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.GeoSystem.Geometry
{
    public class ControlHandle<TPosition> : IPositioned<TPosition>, IEventSubject<int>
    {
        // Front
        public TPosition Position
        {
            get => _position;
            set
            {
                if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
                ValidatePosition(value);
                if (EqualityComparer<TPosition>.Default.Equals(_position, value)) return;
                _position = value;
                _eventHub.Update((int)GeoEvents.Shape);
            }
        }

        // Property
        public IEventHub<int> EventHub => _eventHub;
        public bool IsDisposed { get; private set; }

        // Internal
        private TPosition _position;
        private readonly EventHub<int> _eventHub;


        // Content
        public ControlHandle() => _eventHub = new EventHub<int>(this);
        public ControlHandle(TPosition position) : this() => Position = position;

        protected virtual void ValidatePosition(TPosition position) { }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _eventHub.Dispose();
        }
    }
}
