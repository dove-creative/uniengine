using System.Numerics;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class CapsuleCollider3D : Collider3D
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
        public float Height
        {
            get { ThrowIfInvalid(); return ReadBinding(_height.Read); }
            set
            {
                ThrowIfInvalid();
                SpatialValidation.Nonnegative(value, nameof(value));
                WriteBinding(() => _height.Write(value));
            }
        }
        public Vector3 Center
        {
            get { ThrowIfInvalid(); return ReadBinding(_center.Read); }
            set
            {
                ThrowIfInvalid();
                SpatialValidation.Vector(value, nameof(value));
                WriteBinding(() => _center.Write(value));
            }
        }
        public CapsuleDirection3D Direction
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
        private SpatialProperty<float> _radius;
        private SpatialProperty<float> _height;
        private SpatialProperty<Vector3> _center;
        private SpatialProperty<CapsuleDirection3D> _direction;


        // Content
        public CapsuleCollider3D(
            CollisionSpace3D space,
            ColliderBindings3D bindings,
            SpatialProperty<float> radius,
            SpatialProperty<float> height,
            SpatialProperty<Vector3> center,
            SpatialProperty<CapsuleDirection3D> direction)
            : base(space, Validate(bindings, radius, height, center, direction))
        {
            _radius = radius;
            _height = height;
            _center = center;
            _direction = direction;
        }

        private static ColliderBindings3D Validate(
            ColliderBindings3D bindings,
            SpatialProperty<float> radius,
            SpatialProperty<float> height,
            SpatialProperty<Vector3> center,
            SpatialProperty<CapsuleDirection3D> direction)
        {
            SpatialValidation.Required(radius, nameof(radius));
            SpatialValidation.Required(height, nameof(height));
            SpatialValidation.Required(center, nameof(center));
            SpatialValidation.Required(direction, nameof(direction));
            return SpatialValidation.Required(bindings, nameof(bindings));
        }

        protected override void OnDispose()
        {
            _radius = null;
            _height = null;
            _center = null;
            _direction = null;
            base.OnDispose();
        }
    }
}
