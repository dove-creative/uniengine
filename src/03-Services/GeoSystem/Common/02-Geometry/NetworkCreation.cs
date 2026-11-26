namespace BlackThunder.GeoSystem.Geometry
{
    public readonly struct NetworkCreation<TPosition>
    {
        public INode<TPosition> Node { get; }
        public ISegment<TPosition> Segment { get; }
        public bool IsNewNode { get; }


        public NetworkCreation(INode<TPosition> node, ISegment<TPosition> segment, bool isNewNode)
        {
            Node = node;
            Segment = segment;
            IsNewNode = isNewNode;
        }
    }
}
