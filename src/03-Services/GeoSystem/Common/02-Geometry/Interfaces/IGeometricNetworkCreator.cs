namespace BlackThunder.GeoSystem.Geometry
{
    public interface IGeometricNetworkCreator<TPosition> : INetworkCreator<TPosition> { }

    public interface ILinearGeometricNetworkCreator<TPosition> : IGeometricNetworkCreator<TPosition>
    {
        INode<TPosition> AnchorNode { get; }
        LinearNetworkCreatorState State { get; }
    }

    public interface IBezierGeometricNetworkCreator<TPosition> : IGeometricNetworkCreator<TPosition> { }
}
