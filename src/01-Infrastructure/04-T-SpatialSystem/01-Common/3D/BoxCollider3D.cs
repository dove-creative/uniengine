using System.Numerics;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class BoxCollider3D : Collider3D
    {
        // Front
        public Vector3 Size
        {
            get { ThrowIfInvalid(); return ReadBinding(_size.Read); }
            set
            {
                ThrowIfInvalid();
                SpatialValidation.Size(value, nameof(value));
                WriteBinding(() => _size.Write(value));
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

        // Internal
        private SpatialProperty<Vector3> _size;
        private SpatialProperty<Vector3> _center;


        // Content
        public BoxCollider3D(
            CollisionSpace3D space,
            ColliderBindings3D bindings,
            SpatialProperty<Vector3> size,
            SpatialProperty<Vector3> center)
            : base(space, Validate(bindings, size, center))
        {
            _size = size;
            _center = center;
        }

        private static ColliderBindings3D Validate(
            ColliderBindings3D bindings,
            SpatialProperty<Vector3> size,
            SpatialProperty<Vector3> center)
        {
            SpatialValidation.Required(size, nameof(size));
            SpatialValidation.Required(center, nameof(center));
            return SpatialValidation.Required(bindings, nameof(bindings));
        }

        protected override void OnDispose()
        {
            _size = null;
            _center = null;
            base.OnDispose();
        }
    }
}
