using System.Numerics;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class CircleCollider2D : Collider2D
    {
        // Front
        public float Radius
        {
            get { ThrowIfInvalid(); return ReadBinding(_radius.Read); }
            set
            {
                ThrowIfInvalid();
                SpatialValidation.Nonnegative(value, nameof(value));
                WriteBinding(() => _radius.Write(value));
            }
        }
        public Vector2 Offset
        {
            get { ThrowIfInvalid(); return ReadBinding(_offset.Read); }
            set
            {
                ThrowIfInvalid();
                SpatialValidation.Vector(value, nameof(value));
                WriteBinding(() => _offset.Write(value));
            }
        }

        // Internal
        private SpatialProperty<float> _radius;
        private SpatialProperty<Vector2> _offset;


        // Content
        public CircleCollider2D(
            CollisionSpace2D space,
            ColliderBindings2D bindings,
            SpatialProperty<float> radius,
            SpatialProperty<Vector2> offset)
            : base(space, Validate(bindings, radius, offset))
        {
            _radius = radius;
            _offset = offset;
        }

        private static ColliderBindings2D Validate(
            ColliderBindings2D bindings,
            SpatialProperty<float> radius,
            SpatialProperty<Vector2> offset)
        {
            SpatialValidation.Required(radius, nameof(radius));
            SpatialValidation.Required(offset, nameof(offset));
            return SpatialValidation.Required(bindings, nameof(bindings));
        }

        protected override void OnDispose()
        {
            _radius = null;
            _offset = null;
            base.OnDispose();
        }
    }
}
