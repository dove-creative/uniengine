using System;

namespace BlackThunder.GeoSystem.Geometry
{
    public enum DistanceType
    {
        Length,
        Rate
    }

    public readonly struct Location : IEquatable<Location>
    {
        // Front
        public bool IsForward => !_isBackward;
        public DistanceType DistanceType { get; }
        public float Distance { get; }

        // Internal
        private readonly bool _isBackward;


        // Content
        public Location(float distance, DistanceType distanceType = DistanceType.Length, bool isForward = true)
        {
            if (distanceType != DistanceType.Length && distanceType != DistanceType.Rate)
                throw new ArgumentOutOfRangeException(nameof(distanceType));

            Distance = distance;
            DistanceType = distanceType;
            _isBackward = !isForward;
        }

        public float ToDistance(float segmentLength)
        {
            var distance = DistanceType == DistanceType.Length ? Distance : Distance * segmentLength;
            return IsForward ? distance : segmentLength - distance;
        }

        public float ToRate(float segmentLength) => ToDistance(segmentLength) / segmentLength;

        internal bool TryResolve(float length, float epsilon, out float distance)
        {
            ValidateEpsilon(epsilon);
            distance = ToDistance(length);
            if (!IsFinite(length) || length < 0f || !IsFinite(distance)
                || distance < -epsilon || distance > (double)length + epsilon)
            {
                distance = default;
                return false;
            }

            distance = System.Math.Clamp(distance, 0f, length);
            return true;
        }

        internal static void ValidateEpsilon(float epsilon)
        {
            if (!IsFinite(epsilon) || epsilon < 0f) throw new ArgumentOutOfRangeException(nameof(epsilon));
        }

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public bool Equals(Location other) =>
            IsForward == other.IsForward && DistanceType == other.DistanceType && Distance.Equals(other.Distance);
        public override bool Equals(object obj) => obj is Location other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(IsForward, DistanceType, Distance);
        public static bool operator ==(Location left, Location right) => left.Equals(right);
        public static bool operator !=(Location left, Location right) => !left.Equals(right);
        public override string ToString() => $"[{(IsForward ? "From" : "To")}, {DistanceType}, {Distance}]";
    }
}
