using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    public abstract class LinearSegment<P, R> : Segment<P, R>, ILinearSegment<P, R>
    {
        protected override int OnNodeConnected(INavNode node)
        {
            node.EventHub.Connect(this)
                .OnUpdatedWhere(events => (events & (int)GeoEvents.Shape) != 0, () =>
                {
                    UpdateShape();
                    OnUpdated((int)GeoEvents.Shape);
                });
            UpdateShape();
            return (int)GeoEvents.Shape;
        }

        protected override int OnNodeDisconnected(INavNode node)
        {
            node.EventHub.Disconnect(this);
            UpdateShape();
            return (int)GeoEvents.Shape;
        }

        protected abstract void UpdateShape();
    }
}
