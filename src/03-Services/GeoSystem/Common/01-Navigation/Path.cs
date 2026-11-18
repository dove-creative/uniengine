using System;
using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Navigation
{
    public class Path
    {
        public IReadOnlyNavNode From { get; }
        public IReadOnlyList<Arrow> Arrows { get; }


        public Path(IReadOnlyNavNode from, IEnumerable<Arrow> arrows)
        {
            From = from ?? throw new ArgumentNullException(nameof(from));
            if (from.IsDisposed) throw new ObjectDisposedException(nameof(from));
            if (arrows == null) throw new ArgumentNullException(nameof(arrows));

            var copy = new List<Arrow>();
            var current = from;
            foreach (var arrow in arrows)
            {
                if (arrow == null) throw new ArgumentException("A path cannot contain a null arrow.", nameof(arrows));
                var segment = arrow.Segment;
                if (segment.IsDisposed || arrow.To.IsDisposed
                    || !((ReferenceEquals(segment.From, current) && ReferenceEquals(segment.To, arrow.To))
                        || (ReferenceEquals(segment.To, current) && ReferenceEquals(segment.From, arrow.To))))
                    throw new ArgumentException("The arrows must form a continuous live path.", nameof(arrows));
                copy.Add(arrow);
                current = arrow.To;
            }
            Arrows = copy.AsReadOnly();
        }
    }
}
