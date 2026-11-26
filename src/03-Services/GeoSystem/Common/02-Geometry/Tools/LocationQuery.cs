using System;

namespace BlackThunder.GeoSystem.Geometry
{
    internal struct LocationQuery
    {
        // Front
        public double MinDistance { get; }
        public double MaxDistance { get; }
        public bool SearchForward { get; }
        public bool HasValidLength => Location.IsFinite(_length) && _length > _epsilon;

        // Internal
        private readonly float _length;
        private readonly float _epsilon;
        private readonly bool _isForward;
        private readonly DistanceType _distanceType;
        private double _distance;
        private double _errorSquared;


        // Content
        public LocationQuery(
            float length,
            float startDistance,
            float endDistance,
            bool isForward,
            DistanceType distanceType,
            float epsilon)
        {
            Location.ValidateEpsilon(epsilon);
            if (distanceType != DistanceType.Length && distanceType != DistanceType.Rate)
                throw new ArgumentOutOfRangeException(nameof(distanceType));
            if (!Location.IsFinite(startDistance) || startDistance < 0f
                || (Location.IsFinite(length) && startDistance > length))
                throw new ArgumentOutOfRangeException(nameof(startDistance));
            if (!Location.IsFinite(endDistance) || endDistance < 0f
                || (Location.IsFinite(length) && endDistance > length))
                throw new ArgumentOutOfRangeException(nameof(endDistance));

            MinDistance = Math.Min(startDistance, endDistance);
            MaxDistance = Math.Max(startDistance, endDistance);
            SearchForward = startDistance <= endDistance;
            _length = length;
            _epsilon = epsilon;
            _isForward = isForward;
            _distanceType = distanceType;
            _distance = 0;
            _errorSquared = double.PositiveInfinity;
        }

        public void Consider(double distance, double errorSquared)
        {
            if (!HasValidLength || double.IsNaN(distance) || distance < MinDistance || distance > MaxDistance
                || double.IsNaN(errorSquared) || errorSquared < 0 || errorSquared > (double)_epsilon * _epsilon)
                return;
            if (errorSquared > _errorSquared || (errorSquared == _errorSquared
                && (SearchForward ? distance >= _distance : distance <= _distance)))
                return;

            _distance = distance;
            _errorSquared = errorSquared;
        }

        public bool TryGetResult(out Location location, out double errorSquared)
        {
            location = default;
            errorSquared = default;
            if (double.IsPositiveInfinity(_errorSquared)) return false;

            var directed = _isForward ? _distance : _length - _distance;
            var value = _distanceType == DistanceType.Length ? directed : directed / _length;
            location = new Location((float)value, _distanceType, _isForward);
            errorSquared = _errorSquared;
            return true;
        }
    }
}
