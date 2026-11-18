using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackThunder.GeoSystem.Geometry
{
    public class Path<TPosition> : Navigation.Path
    {
        public new IReadOnlyNode<TPosition> From => (IReadOnlyNode<TPosition>)base.From;
        public new IReadOnlyList<Arrow<TPosition>> Arrows { get; }


        public Path(IReadOnlyNode<TPosition> from, IEnumerable<Arrow<TPosition>> arrows) : base(from, arrows)
        {
            Arrows = Array.AsReadOnly(base.Arrows.Cast<Arrow<TPosition>>().ToArray());
        }
    }
}
