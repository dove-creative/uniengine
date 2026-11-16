namespace BlackThunder.GeoSystem.Navigation
{
    internal interface INavNodeConnections
    {
        void ValidateSegment(INavSegment segment);
        void ConnectSegmentInternal(INavSegment segment);
        void DisconnectSegmentInternal(INavSegment segment);
        void NotifyConnectionChanged(int events);
    }
}
