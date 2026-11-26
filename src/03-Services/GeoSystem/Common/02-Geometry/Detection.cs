namespace BlackThunder.GeoSystem.Geometry
{
    public readonly struct Detection<TPosition>
    {
        public TPosition Position { get; }
        public INode<TPosition> Node { get; }
        public IReadOnlySegment<TPosition> Segment { get; }


        public Detection(
            TPosition position,
            INode<TPosition> node = null,
            IReadOnlySegment<TPosition> segment = null)
        {
            Position = position;
            Node = node;
            Segment = segment;
        }
    }
}
