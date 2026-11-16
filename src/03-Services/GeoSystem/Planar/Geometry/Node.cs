using System;
using System.Numerics;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public class Node : PositionedNode
    {
        public new Vector2 Position
        {
            get => base.Position;
            set
            {
                ThrowIfDisposed();
                if (!Location.IsFinite(value.X) || !Location.IsFinite(value.Y))
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (base.Position == value) return;

                base.Position = value;
                OnUpdated((int)GeoEvents.Shape);
            }
        }


        public Node() { }
        public Node(Vector2 position) => Position = position;
        public static implicit operator Vector2(Node node) => node.Position;
    }
}
