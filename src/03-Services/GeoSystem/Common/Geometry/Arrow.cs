namespace BlackThunder.GeoSystem.Geometry
{
    public class Arrow<P> : Navigation.Arrow
    {
        public new IReadOnlySegment<P> Segment => (IReadOnlySegment<P>)base.Segment;
        public new IReadOnlyNode<P> To => (IReadOnlyNode<P>)base.To;


        public Arrow(IReadOnlySegment<P> segment, IReadOnlyNode<P> to) : base(segment, to) { }
    }
}
