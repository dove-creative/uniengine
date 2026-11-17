using System;
using System.Collections.Generic;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.GeoSystem.Geometry
{
    public class SegmentHandle<P> : IPositioned<P>, IEventSubject<int>
    {
        // Front
        public P Position
        {
            get => _position;
            set
            {
                if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
                ValidatePosition(value);
                if (EqualityComparer<P>.Default.Equals(_position, value)) return;
                _position = value;
                _eventHub.Update((int)GeoEvents.Shape);
            }
        }

        // Property
        public IEventHub<int> EventHub => _eventHub;
        public bool IsDisposed { get; private set; }

        // Internal
        private P _position;
        private readonly EventHub<int> _eventHub;


        // Content
        public SegmentHandle() => _eventHub = new EventHub<int>(this);
        public SegmentHandle(P position) : this() => Position = position;

        protected virtual void ValidatePosition(P position) { }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _eventHub.Dispose();
        }
    }
}
