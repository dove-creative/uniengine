using System;

namespace BlackThunder.GeoSystem.Navigation
{
    public class Arrow
    {
        public IReadOnlyNavSegment Segment { get; }
        public IReadOnlyNavNode To { get; }

        public Arrow(IReadOnlyNavSegment segment, IReadOnlyNavNode to)
        {
            Segment = segment ?? throw new ArgumentNullException(nameof(segment));
            To = to ?? throw new ArgumentNullException(nameof(to));

            if (segment.IsDisposed) throw new ObjectDisposedException(nameof(segment));
            if (to.IsDisposed) throw new ObjectDisposedException(nameof(to));
            if (segment.From == null || segment.To == null
                || ReferenceEquals(segment.From, segment.To)
                || (!ReferenceEquals(segment.From, to) && !ReferenceEquals(segment.To, to)))
                throw new ArgumentException("The destination must be an endpoint of a complete segment.", nameof(to));
        }
    }
}
