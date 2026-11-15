namespace BlackThunder.UniEngine.Core
{
    /// <summary>
    /// Supplies values and owns the lifetime of an enumeration.
    /// </summary>
    /// <remarks>
    /// Enumeration identifiers must not be reused while old enumerators can still exist.
    /// A failed begin operation must leave existing enumerations unchanged.
    /// </remarks>
    public interface IEnumerationSource<T>
    {
        /// <summary>
        /// Starts an enumeration and returns its identifier.
        /// </summary>
        long BeginEnumeration();

        /// <summary>
        /// Advances an active enumeration, rejecting identifiers that are no longer active.
        /// Returning false does not end the enumeration; the caller must still end it.
        /// </summary>
        bool TryMoveNext(long enumerationId, out T current);

        /// <summary>
        /// Ends an active enumeration and releases its state even if cleanup fails.
        /// Repeated calls and identifiers that are no longer active must be ignored.
        /// </summary>
        void EndEnumeration(long enumerationId);
    }
}
