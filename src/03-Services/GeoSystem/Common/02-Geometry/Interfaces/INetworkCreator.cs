using System;

namespace BlackThunder.GeoSystem.Geometry
{
    // Factories transfer only newly owned objects through their out parameters, even on failure.
    // They must clean up allocations that have not been assigned to an out parameter before throwing.
    public delegate bool NetworkNodeFactory<TPosition>(Detection<TPosition> detection, out INode<TPosition> node);

    public delegate bool NetworkSegmentFactory<TPosition>(
        INode<TPosition> from,
        INode<TPosition> to,
        out ISegment<TPosition> segment);

    public interface INetworkCreator<TPosition> : IDisposable
    {
        NetworkNodeFactory<TPosition> NodeFactory { get; }
        NetworkSegmentFactory<TPosition> SegmentFactory { get; }
        Func<Detection<TPosition>> GetCurrentDetections { get; }
        Detection<TPosition> CurrentDetection { get; }
        bool IsCreating { get; }
        bool IsDisposed { get; }

        void Begin();
        void Update();
        bool TryConfirm(out NetworkCreation<TPosition> creation);
        void Cancel();
        void Finish();
    }
}
