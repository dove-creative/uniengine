using System.Numerics;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class SphereCollider3D : Collider3D
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
        private SpatialProperty<float> _radius;
        private SpatialProperty<Vector3> _center;


        // Content
        public SphereCollider3D(
            CollisionSpace3D space,
            ColliderBindings3D bindings,
            SpatialProperty<float> radius,
            SpatialProperty<Vector3> center)
            : base(space, Validate(bindings, radius, center))
        {
            _radius = radius;
            _center = center;
        }

        private static ColliderBindings3D Validate(
            ColliderBindings3D bindings,
            SpatialProperty<float> radius,
            SpatialProperty<Vector3> center)
        {
            SpatialValidation.Required(radius, nameof(radius));
            SpatialValidation.Required(center, nameof(center));
            return SpatialValidation.Required(bindings, nameof(bindings));
        }

        protected override void OnDispose()
        {
            _radius = null;
            _center = null;
            base.OnDispose();
        }
    }
}
