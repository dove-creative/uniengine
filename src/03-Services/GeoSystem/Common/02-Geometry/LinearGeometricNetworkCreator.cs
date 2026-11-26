using System;

namespace BlackThunder.GeoSystem.Geometry
{
    public enum LinearNetworkCreatorState { Idle, PutFirstNode, SelectFirstNode, PutNextNode, SelectNextNode }

    public class LinearGeometricNetworkCreator<TPosition> : ILinearGeometricNetworkCreator<TPosition>
    {
        // Front
        public INode<TPosition> AnchorNode { get; private set; }
        public Detection<TPosition> CurrentDetection { get; private set; }
        public LinearNetworkCreatorState State { get; private set; }
        public bool IsCreating { get; private set; }

        // Property
        public bool IsDisposed { get; private set; }

        // Control
        public NetworkNodeFactory<TPosition> NodeFactory { get; }
        public NetworkSegmentFactory<TPosition> SegmentFactory { get; }
        public Func<Detection<TPosition>> GetCurrentDetections { get; }

        // Internal
        private readonly LinearNetworkCreatorStateMachine _machine = new();
        private bool _editing;


        // Content --------------------
        // Construction
        public LinearGeometricNetworkCreator(
            Func<Detection<TPosition>> getCurrentDetections,
            NetworkNodeFactory<TPosition> nodeFactory,
            NetworkSegmentFactory<TPosition> segmentFactory)
        {
            GetCurrentDetections = getCurrentDetections
                ?? throw new ArgumentNullException(nameof(getCurrentDetections));
            NodeFactory = nodeFactory ?? throw new ArgumentNullException(nameof(nodeFactory));
            SegmentFactory = segmentFactory ?? throw new ArgumentNullException(nameof(segmentFactory));
        }

        // Editing
        public void Begin()
        {
            EnterEdit();
            try
            {
                if (IsCreating) return;
                var detection = GetCurrentDetections();
                CurrentDetection = detection;
                IsCreating = true;
                SelectState();
            }
            finally { _editing = false; }
        }

        public void Update()
        {
            EnterEdit();
            try
            {
                if (IsCreating) RefreshDetection();
            }
            finally { _editing = false; }
        }

        public bool TryConfirm(out NetworkCreation<TPosition> creation)
        {
            creation = default;
            EnterEdit();
            INode<TPosition> node = null;
            ISegment<TPosition> segment = null;
            var ownsNode = false;
            var committed = false;
            try
            {
                if (!IsCreating) return false;
                RefreshDetection();
                var detection = CurrentDetection;
                var selected = detection.Node != null;
                if (selected && detection.Node.IsDisposed) return false;
                if (!selected && detection.Segment != null && detection.Segment.IsDisposed) return false;

                if (selected) node = detection.Node;
                else
                {
                    ownsNode = true;
                    if (!NodeFactory(detection, out node)) return false;
                    if (node == null || node.IsDisposed)
                        throw new InvalidOperationException("The node factory must return a live new node on success.");
                    if (ReferenceEquals(node, AnchorNode))
                    {
                        ownsNode = false;
                        throw new InvalidOperationException("The node factory must not return the existing anchor.");
                    }
                }

                if (AnchorNode != null)
                {
                    if (ReferenceEquals(AnchorNode, node)) return false;
                    if (!SegmentFactory(AnchorNode, node, out segment)) return false;
                    if (segment == null || segment.IsDisposed || segment is not ILinearSegment
                        || !ReferenceEquals(segment.From, AnchorNode) || !ReferenceEquals(segment.To, node))
                    {
                        throw new InvalidOperationException(
                            "The factory must return a live linear segment connecting the requested endpoints.");
                    }
                    if (AnchorNode.IsDisposed || node.IsDisposed)
                        throw new InvalidOperationException("A factory disposed an endpoint during creation.");
                }

                AnchorNode = node;
                creation = new NetworkCreation<TPosition>(node, segment, ownsNode);
                committed = true;
                SelectState();
                return true;
            }
            finally
            {
                try
                {
                    if (!committed)
                    {
                        try { segment?.Dispose(); }
                        finally
                        {
                            if (ownsNode && !ReferenceEquals(node, AnchorNode)) node?.Dispose();
                        }
                    }
                }
                finally { _editing = false; }
            }
        }

        public void Cancel()
        {
            EnterEdit();
            try
            {
                if (AnchorNode != null) AnchorNode = null;
                else IsCreating = false;
                SelectState();
                if (!IsCreating) CurrentDetection = default;
            }
            finally { _editing = false; }
        }

        public void Finish()
        {
            EnterEdit();
            try { Reset(); }
            finally { _editing = false; }
        }

        // State and lifetime
        private void RefreshDetection()
        {
            var detection = GetCurrentDetections();
            CurrentDetection = detection;
            if (AnchorNode != null && AnchorNode.IsDisposed) AnchorNode = null;
            SelectState();
        }

        private void SelectState() =>
            State = _machine.Select(IsCreating, AnchorNode != null, CurrentDetection.Node != null);

        private void Reset()
        {
            IsCreating = false;
            AnchorNode = null;
            CurrentDetection = default;
            SelectState();
        }

        private void EnterEdit()
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            if (_editing) throw new InvalidOperationException("Creator edits cannot be reentered from a callback.");
            _editing = true;
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            EnterEdit();
            try
            {
                Reset();
                IsDisposed = true;
            }
            finally { _editing = false; }
        }
    }
}
