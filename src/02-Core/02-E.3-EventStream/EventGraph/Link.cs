namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        internal abstract class Link<TLink> where TLink : Link<TLink>
        {
            internal long Registration;
            internal TLink Previous;
            internal TLink Next;
        }

        internal struct Chain<TLink> where TLink : Link<TLink>
        {
            internal TLink First { get; private set; }
            private TLink _last;

            internal void Insert(TLink link, TLink before = null)
            {
                link.Next = before;
                link.Previous = before == null ? _last : before.Previous;

                if (link.Previous == null) First = link;
                else link.Previous.Next = link;

                if (before == null) _last = link;
                else before.Previous = link;
            }

            internal void Remove(TLink link)
            {
                if (link.Previous == null) First = link.Next;
                else link.Previous.Next = link.Next;

                if (link.Next == null) _last = link.Previous;
                else link.Next.Previous = link.Previous;

                link.Previous = null;
                // A cursor on a removed item can still reach the following live sibling.
            }
        }
    }
}
