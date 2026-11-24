using System;
using System.Numerics;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public enum CapsuleDirection2D { Horizontal, Vertical }
    public enum CapsuleDirection3D { X, Y, Z }

    public readonly struct SpatialQueryFilter
    {
        public int LayerMask { get; }
        public bool IncludeTriggers { get; }
        public static SpatialQueryFilter Default => new(-1, true);


        public SpatialQueryFilter(int layerMask, bool includeTriggers = true) =>
            (LayerMask, IncludeTriggers) = (layerMask, includeTriggers);
    }

    public class SpatialProperty<T>
    {
        public Func<T> Read { get; }
        public Action<T> Write { get; }


        public SpatialProperty(Func<T> read, Action<T> write)
        {
            Read = read ?? throw new ArgumentNullException(nameof(read));
            Write = write ?? throw new ArgumentNullException(nameof(write));
        }
    }

    internal static class SpatialValidation
    {
        public static T Required<T>(T value, string name) where T : class =>
            value ?? throw new ArgumentNullException(name);

        public static void Finite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "The value must be finite.");
        }

        public static void Nonnegative(float value, string name)
        {
            Finite(value, name);

            if (value < 0f) throw new ArgumentOutOfRangeException(name, "The value must be nonnegative.");
        }

        public static void Positive(float value, string name)
        {
            Finite(value, name);

            if (value <= 0f) throw new ArgumentOutOfRangeException(name, "The value must be positive.");
        }

        public static void Layer(int value, string name)
        {
            if (value < 0 || value > 31) throw new ArgumentOutOfRangeException(name);
        }

        public static void Distance(float value, string name)
        {
            if (float.IsNaN(value) || value < 0f)
                throw new ArgumentOutOfRangeException(name, "The distance must be nonnegative.");
        }

        public static Vector2 Direction(Vector2 value, string name)
        {
            var length = Math.Sqrt((double)value.X * value.X + (double)value.Y * value.Y);
            if (length == 0) throw new ArgumentOutOfRangeException(name);
            return new Vector2((float)(value.X / length), (float)(value.Y / length));
        }

        public static Vector3 Direction(Vector3 value, string name)
        {
            var length = Math.Sqrt((double)value.X * value.X + (double)value.Y * value.Y + (double)value.Z * value.Z);
            if (length == 0) throw new ArgumentOutOfRangeException(name);
            return new Vector3((float)(value.X / length), (float)(value.Y / length), (float)(value.Z / length));
        }

        public static void Vector(Vector2 value, string name)
        {
            Finite(value.X, name);
            Finite(value.Y, name);
        }

        public static void Vector(Vector3 value, string name)
        {
            Finite(value.X, name);
            Finite(value.Y, name);
            Finite(value.Z, name);
        }

        public static void Size(Vector2 value, string name)
        {
            Nonnegative(value.X, name);
            Nonnegative(value.Y, name);
        }

        public static void Size(Vector3 value, string name)
        {
            Nonnegative(value.X, name);
            Nonnegative(value.Y, name);
            Nonnegative(value.Z, name);
        }

        public static void Pose(Transform2D value, string name)
        {
            Vector(value.Position, name);
            Finite(value.Rotation, name);
        }

        public static void Pose(Transform3D value, string name)
        {
            Vector(value.Position, name);
            var rotation = value.Rotation;
            Finite(rotation.X, name);
            Finite(rotation.Y, name);
            Finite(rotation.Z, name);
            Finite(rotation.W, name);
            var length = rotation.LengthSquared();

            if (length == 0f || float.IsInfinity(length))
                throw new ArgumentOutOfRangeException(name, "The rotation must have finite nonzero length.");
        }

        public static void EnumValue<T>(T value, string name) where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value)) throw new ArgumentOutOfRangeException(name);
        }
    }
}
