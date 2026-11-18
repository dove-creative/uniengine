namespace BlackThunder.GeoSystem.Geometry
{
    internal readonly struct Hinge<TPosition, TRotation>
    {
        public TPosition Position { get; }
        public TRotation Rotation { get; }
        public float Distance { get; }

        public Hinge(TPosition position, TRotation rotation, float distance)
        {
            Position = position;
            Rotation = rotation;
            Distance = distance;
        }
    }
}
