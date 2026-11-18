namespace BlackThunder.GeoSystem.Geometry
{
    public class Arrow<TPosition> : Navigation.Arrow
    {
        public new IReadOnlySegment<TPosition> Segment => (IReadOnlySegment<TPosition>)base.Segment;
        public new IReadOnlyNode<TPosition> To => (IReadOnlyNode<TPosition>)base.To;

        public Arrow(IReadOnlySegment<TPosition> segment, IReadOnlyNode<TPosition> to) : base(segment, to) { }
    }
}
