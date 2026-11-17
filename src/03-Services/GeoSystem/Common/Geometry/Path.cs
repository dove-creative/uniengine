using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackThunder.GeoSystem.Geometry
{
    public class Path<P> : Navigation.Path
    {
        public new IReadOnlyNode<P> From => (IReadOnlyNode<P>)base.From;
        public new IReadOnlyList<Arrow<P>> Arrows { get; }


        public Path(IReadOnlyNode<P> from, IEnumerable<Arrow<P>> arrows) : base(from, arrows)
        {
            Arrows = Array.AsReadOnly(base.Arrows.Cast<Arrow<P>>().ToArray());
        }
    }
}
