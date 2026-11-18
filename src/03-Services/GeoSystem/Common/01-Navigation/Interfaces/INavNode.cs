using System.Collections.Generic;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.GeoSystem.Navigation
{
    public interface IReadOnlyNavNode : IEventSubject<int>, IPropertySubject
    {
        IEnumerable<IReadOnlyNavSegment> Segments { get; }
        bool IsIsolated { get; }
        string FormatLogMessage(string message);
    }

    public interface INavNode : IReadOnlyNavNode
    {
        new IEnumerable<INavSegment> Segments { get; }

        void ConnectSegment(INavSegment segment, ConnectionSide side);
        void DisconnectSegment(INavSegment segment);
        void ClearSegments();
    }
}
