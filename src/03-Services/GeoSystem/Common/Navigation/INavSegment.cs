using System.Collections.Generic;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.GeoSystem.Navigation
{
    public interface IReadOnlyNavSegment : IEventSubject<int>
    {
        IReadOnlyNavNode From { get; }
        IReadOnlyNavNode To { get; }
        IEnumerable<IReadOnlyNavNode> Nodes { get; }
        bool IsIsolated { get; }
        float Length { get; }
        bool IsFromNode(IReadOnlyNavNode node);
        IReadOnlyNavNode GetCounterNode(IReadOnlyNavNode node);
        string FormatLogMessage(string message);
    }

    public interface INavSegment : IReadOnlyNavSegment
    {
        new INavNode From { get; }
        new INavNode To { get; }
        new IEnumerable<INavNode> Nodes { get; }
        IReadOnlyNavNode IReadOnlyNavSegment.From => From;
        IReadOnlyNavNode IReadOnlyNavSegment.To => To;
        IEnumerable<IReadOnlyNavNode> IReadOnlyNavSegment.Nodes => Nodes;

        void ConnectNode(ConnectionSide side, INavNode node);
        void DisconnectNode(ConnectionSide side);
        void DisconnectNode(INavNode node);
        void ClearNodes();
    }
}
