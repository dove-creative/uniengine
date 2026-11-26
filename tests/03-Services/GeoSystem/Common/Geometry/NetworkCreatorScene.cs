using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using PlanarNode = BlackThunder.GeoSystem.Geometry.Planar.Node;
using PlanarSegment = BlackThunder.GeoSystem.Geometry.Planar.LinearSegment;

namespace BlackThunder.UniEngine.Tests
{
    internal class NetworkCreatorScene : IDisposable
    {
        public Detection<Vector2> Detection { get; set; }
        public LinearGeometricNetworkCreator<Vector2> Creator { get; }
        public List<INode<Vector2>> Nodes { get; } = new();
        public List<ISegment<Vector2>> Segments { get; } = new();
        public bool RejectNode { get; set; }
        public bool RejectSegment { get; set; }
        public bool ThrowNode { get; set; }
        public bool ThrowSegment { get; set; }
        public Action Detecting { get; set; }
        public Action NodeCreated { get; set; }
        public Action SegmentCreated { get; set; }
        public int DetectionCount { get; private set; }


        public NetworkCreatorScene() => Creator = new(GetDetection, CreateNode, CreateSegment);

        private Detection<Vector2> GetDetection()
        {
            DetectionCount++;
            Detecting?.Invoke();
            return Detection;
        }

        private bool CreateNode(Detection<Vector2> detection, out INode<Vector2> node)
        {
            node = new PlanarNode(detection.Position);
            Nodes.Add(node);
            NodeCreated?.Invoke();
            if (ThrowNode) throw new InvalidOperationException("Node creation failed.");
            return !RejectNode;
        }

        private bool CreateSegment(INode<Vector2> from, INode<Vector2> to, out ISegment<Vector2> segment)
        {
            segment = new PlanarSegment();
            Segments.Add(segment);
            segment.ConnectNode(ConnectionSide.From, from);
            SegmentCreated?.Invoke();
            if (ThrowSegment) throw new InvalidOperationException("Segment creation failed.");
            segment.ConnectNode(ConnectionSide.To, to);
            return !RejectSegment;
        }

        public void Dispose()
        {
            Creator.Dispose();
            foreach (var segment in Segments) segment.Dispose();
            foreach (var node in Nodes) node.Dispose();
        }
    }
}
