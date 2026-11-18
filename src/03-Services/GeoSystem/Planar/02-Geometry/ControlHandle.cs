using System;
using System.Numerics;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public class ControlHandle : Geometry.ControlHandle<Vector2>
    {
        public ControlHandle() { }
        public ControlHandle(Vector2 position) : base(position) { }

        protected override void ValidatePosition(Vector2 position)
        {
            if (!Location.IsFinite(position.X) || !Location.IsFinite(position.Y))
                throw new ArgumentOutOfRangeException(nameof(position));
        }
    }
}
