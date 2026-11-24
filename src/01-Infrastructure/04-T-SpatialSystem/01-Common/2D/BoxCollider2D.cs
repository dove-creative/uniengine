using System.Numerics;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class BoxCollider2D : Collider2D
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

        // Internal
        private SpatialProperty<Vector2> _size;
        private SpatialProperty<Vector2> _offset;


        // Content
        public BoxCollider2D(
            CollisionSpace2D space,
            ColliderBindings2D bindings,
            SpatialProperty<Vector2> size,
            SpatialProperty<Vector2> offset)
            : base(space, Validate(bindings, size, offset))
        {
            _size = size;
            _offset = offset;
        }

        private static ColliderBindings2D Validate(
            ColliderBindings2D bindings,
            SpatialProperty<Vector2> size,
            SpatialProperty<Vector2> offset)
        {
            SpatialValidation.Required(size, nameof(size));
            SpatialValidation.Required(offset, nameof(offset));
            return SpatialValidation.Required(bindings, nameof(bindings));
        }

        protected override void OnDispose()
        {
            _size = null;
            _offset = null;
            base.OnDispose();
        }
    }
}
