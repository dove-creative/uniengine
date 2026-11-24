using System.Numerics;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class CapsuleCollider2D : Collider2D
    {
        // Front
        public Vector2 Size
        {
            get { ThrowIfInvalid(); return ReadBinding(_size.Read); }
            set
            {
                ThrowIfInvalid();
                SpatialValidation.Size(value, nameof(value));
                WriteBinding(() => _size.Write(value));
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
        public CapsuleDirection2D Direction
        {
            get { ThrowIfInvalid(); return ReadBinding(_direction.Read); }
            set
            {
                ThrowIfInvalid();
                SpatialValidation.EnumValue(value, nameof(value));
                WriteBinding(() => _direction.Write(value));
            }
        }

        // Internal
        private SpatialProperty<Vector2> _size;
        private SpatialProperty<Vector2> _offset;
        private SpatialProperty<CapsuleDirection2D> _direction;


        // Content
        public CapsuleCollider2D(
            CollisionSpace2D space,
            ColliderBindings2D bindings,
            SpatialProperty<Vector2> size,
            SpatialProperty<Vector2> offset,
            SpatialProperty<CapsuleDirection2D> direction)
            : base(space, Validate(bindings, size, offset, direction))
        {
            _size = size;
            _offset = offset;
            _direction = direction;
        }

        private static ColliderBindings2D Validate(
            ColliderBindings2D bindings,
            SpatialProperty<Vector2> size,
            SpatialProperty<Vector2> offset,
            SpatialProperty<CapsuleDirection2D> direction)
        {
            SpatialValidation.Required(size, nameof(size));
            SpatialValidation.Required(offset, nameof(offset));
            SpatialValidation.Required(direction, nameof(direction));
            return SpatialValidation.Required(bindings, nameof(bindings));
        }

        protected override void OnDispose()
        {
            _size = null;
            _offset = null;
            _direction = null;
            base.OnDispose();
        }
    }
}
