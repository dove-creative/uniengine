namespace BlackThunder.GeoSystem.Geometry
{
    public readonly struct Hinge<P, R>
    {
        public P Position { get; }
        public R Rotation { get; }
        public float Distance { get; }


        public Hinge(P position, R rotation, float distance)
        {
            Position = position;
            Rotation = rotation;
            Distance = distance;
        }
    }
}
