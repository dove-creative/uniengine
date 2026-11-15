using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using BlackThunder.UniEngine.StateMachines.Usm.Core;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public class Node : INode
    {
        // Front
        public string Id { get; }
        public IReadOnlyList<INode> Children { get; }
        public IWork Work { get; }
        public ITransitioner Transitioner { get; }
        public AbortPolicies AbortPolicies { get; }
        public bool IsActive { get; private set; }
        public OperationInfo LastResult { get; private set; }

        // Control
        public ReframeOption ReframeOption
        {
            get => _reframe;
            set
            {
                if (value < ReframeOption.None || value > ReframeOption.CheckConditionThenUpdate)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _reframe = value;
                _reframeVersion++;
            }
        }

        // Internal
        private static readonly ConditionalWeakTable<object, Node> _owners = new();
        private readonly Node[] _children;
        private Node _parent;
        private int _initialization;
        private int _updating;
        private bool _stopping;
        private bool _cancelled;
        private bool _workActive;
        private bool _transitionerActive;
        private ReframeOption _reframe;
        private long _reframeVersion;
        private Node Root => _parent?.Root ?? this;

        private class IdentityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }


        // Content --------------------
        // Construction
        public Node(
            string id,
            IWork work = null,
            ITransitioner transitioner = null,
            IEnumerable<Node> children = null,
            AbortPolicies abortPolicies = AbortPolicies.None)
        {
            Id = StateMachineNames.GetName(id);
            if ((abortPolicies & ~(AbortPolicies.Self | AbortPolicies.LowerPriority | AbortPolicies.StopOnFailure)) != 0)
                throw new ArgumentOutOfRangeException(nameof(abortPolicies));

            Work = work ?? new Work();
            Transitioner = transitioner;
            AbortPolicies = abortPolicies;
            _children = children?.ToArray() ?? Array.Empty<Node>();
            if (_children.Any(child => child == null)) throw new ArgumentException("A child cannot be null.", nameof(children));
            Children = Array.AsReadOnly(_children);
        }

        public void Initialize(object owner = null, object blackboard = null)
        {
            if (_parent != null || Interlocked.CompareExchange(ref _initialization, 1, 0) != 0)
                throw new InvalidOperationException("Initialize the root exactly once.");

            var nodes = new List<Node>();
            var visited = new HashSet<object>(new IdentityComparer());
            var resources = new HashSet<object>(new IdentityComparer());

            try
            {
                Validate(this);
                foreach (var node in nodes)
                {
                    _owners.Add(node.Work, node);
                    if (node.Transitioner != null) _owners.Add(node.Transitioner, node);
                    foreach (var child in node._children) child._parent = node;
                }

                InitializeTree(this);
                _initialization = 2;
            }
            catch
            {
                _initialization = 3;
                throw;
            }

            void Validate(Node node)
            {
                if (!visited.Add(node)) throw new InvalidOperationException("Cycles and shared children are not allowed.");
                if (node._parent != null || (!ReferenceEquals(node, this) && node._initialization != 0))
                    throw new InvalidOperationException("A node already belongs to an initialized or failed tree.");
                if (node._children.Length > 0 && node.Transitioner == null)
                    throw new InvalidOperationException("A node with children requires a transitioner.");

                Claim(node.Work);
                if (node.Transitioner != null) Claim(node.Transitioner);
                nodes.Add(node);
                var ids = new HashSet<string>(StringComparer.Ordinal);

                foreach (var child in node._children)
                {
                    if (!ids.Add(child.Id)) throw new InvalidOperationException("Sibling IDs must be unique.");
                    Validate(child);
                }
            }

            void Claim(object resource)
            {
                if (!resources.Add(resource) || _owners.TryGetValue(resource, out _))
                    throw new InvalidOperationException("Work and transitioner instances cannot be shared.");
            }

            void InitializeTree(Node node)
            {
                foreach (var child in node._children) InitializeTree(child);
                node.Work.Initialize(owner, blackboard);
                node.Transitioner?.Initialize(owner, blackboard, node.Children);
            }
        }

        // Execution
        public bool CheckCondition(object input)
        {
            RequireInitialized();
            return Work.CheckCondition(input);
        }

        public OperationInfo Update(IFrameContext context)
        {
            RequireInitialized();
            var frame = FrameContext.Require(context);
            for (var ancestor = _parent; ancestor != null; ancestor = ancestor._parent)
            {
                if (!ancestor.IsActive)
                    throw new InvalidOperationException("A child cannot run while an ancestor is inactive.");
            }

            if (_stopping || Root._stopping || Interlocked.CompareExchange(ref _updating, 1, 0) != 0)
                throw new InvalidOperationException("Node updates cannot overlap or run during stopping.");

            _cancelled = false;
            try
            {
                RunOnce(frame);
                while (!_cancelled && _reframe != ReframeOption.None)
                {
                    var request = _reframe;
                    var version = _reframeVersion;

                    if (request == ReframeOption.CheckConditionThenUpdate)
                    {
                        var accepted = CheckCondition(frame.Payload);
                        if (_cancelled) break;
                        if (!accepted)
                        {
                            RejectCondition(frame);
                            _reframe = ReframeOption.None;
                            break;
                        }
                    }

                    RunOnce(frame);
                    // A callback may replace even an identical request while this one is being consumed.
                    if (_reframeVersion == version) _reframe = ReframeOption.None;
                }

                return LastResult;
            }
            catch (Exception error)
            {
                _reframe = ReframeOption.None;
                LastResult = new OperationInfo(DetailedOperationStatus.Failure, frame.Payload);
                frame.Report(ElementType.Node, FrameType.Update, ExecutionPhase.Exited, LastResult);
                try { StopCore(frame, DetailedOperationStatus.Failure); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
            finally
            {
                if (_cancelled) _reframe = ReframeOption.None;
                Volatile.Write(ref _updating, 0);
            }
        }

        private void RunOnce(FrameContext frame)
        {
            frame.Id = Id;
            frame.ConsumeUpdate();
            frame.Report(ElementType.Node, FrameType.Update, ExecutionPhase.Entered);
            IsActive = true;
            LastResult = new OperationInfo(DetailedOperationStatus.Running, frame.Payload);

            if (!_workActive)
            {
                _workActive = true;
                frame.Report(ElementType.Work, FrameType.Start, ExecutionPhase.Entered);
                Work.Start(frame.Payload);
                frame.Report(ElementType.Work, FrameType.Start, ExecutionPhase.Exited);
                if (!IsActive) return;
            }

            frame.Report(ElementType.Work, FrameType.Update, ExecutionPhase.Entered);
            var result = Work.Update(frame.Payload);
            frame.Report(ElementType.Work, FrameType.Update, ExecutionPhase.Exited, result);
            if (!IsActive) return;
            Accept(result);

            if (result.IsRunning && Transitioner != null)
            {
                if (!_transitionerActive)
                {
                    _transitionerActive = true;
                    frame.Report(ElementType.Transitioner, FrameType.Start, ExecutionPhase.Entered);
                    Transitioner.Start(frame.Payload, frame);
                    frame.Report(ElementType.Transitioner, FrameType.Start, ExecutionPhase.Exited);
                    if (!IsActive) return;
                }

                frame.Report(ElementType.Transitioner, FrameType.Update, ExecutionPhase.Entered);
                result = Transitioner.Update(frame);
                frame.Report(ElementType.Transitioner, FrameType.Update, ExecutionPhase.Exited, result);
                if (!IsActive) return;
                Accept(result);
            }

            frame.Report(ElementType.Node, FrameType.Update, ExecutionPhase.Exited, LastResult);
            if (LastResult.IsStopped) StopCore(frame, LastResult.DetailedOperationStatus);

            void Accept(OperationInfo info)
            {
                if (info.OperationStatus == OperationStatus.NeverRun)
                    throw new InvalidOperationException("Update must return a running or terminal operation.");

                LastResult = info;
                frame.Payload = info.Result;
            }
        }

        internal OperationInfo RejectCondition(FrameContext frame)
        {
            frame.Id = Id;
            frame.ConsumeUpdate();
            LastResult = new OperationInfo(DetailedOperationStatus.HaltedBySelf, frame.Payload);
            _reframe = ReframeOption.None;
            StopCore(frame, DetailedOperationStatus.HaltedBySelf);
            frame.Report(ElementType.Node, FrameType.Update, ExecutionPhase.Exited, LastResult);
            return LastResult;
        }

        public void Stop(
            IFrameContext context,
            DetailedOperationStatus reason = DetailedOperationStatus.HaltedByExternal)
        {
            RequireInitialized();
            var frame = FrameContext.Require(context);
            if (!new OperationInfo(reason).IsStopped)
                throw new ArgumentException("A stop reason must be terminal.", nameof(reason));

            if (!IsActive && _updating == 0) return;
            _cancelled = true;
            _reframe = ReframeOption.None;

            try { StopCore(frame, reason); }
            finally
            {
                if (_cancelled) _reframe = ReframeOption.None;
            }
        }

        private void StopCore(FrameContext frame, DetailedOperationStatus reason)
        {
            if (!IsActive) return;
            IsActive = false;
            LastResult = new OperationInfo(reason, LastResult.Result);
            _stopping = true;
            frame.Id = Id;
            frame.Report(ElementType.Node, FrameType.Stop, ExecutionPhase.Entered);
            List<Exception> errors = null;

            try
            {
                foreach (var child in _children)
                {
                    if (!child.IsActive) continue;
                    try
                    {
                        child.Stop(
                            frame.GetOrAddNext(child.Id, child.LastResult.Result),
                            DetailedOperationStatus.HaltedByParent);
                    }
                    catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
                }

                if (_transitionerActive)
                {
                    _transitionerActive = false;
                    frame.Report(ElementType.Transitioner, FrameType.Stop, ExecutionPhase.Entered);
                    try { Transitioner.Stop(reason, frame); }
                    catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
                    frame.Report(ElementType.Transitioner, FrameType.Stop, ExecutionPhase.Exited, LastResult);
                }

                if (_workActive)
                {
                    _workActive = false;
                    frame.Report(ElementType.Work, FrameType.Stop, ExecutionPhase.Entered);
                    try { Work.Stop(reason); }
                    catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
                    frame.Report(ElementType.Work, FrameType.Stop, ExecutionPhase.Exited, LastResult);
                }
            }
            finally
            {
                _stopping = false;
                frame.Report(ElementType.Node, FrameType.Stop, ExecutionPhase.Exited, LastResult);
            }

            if (errors?.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors != null) throw new AggregateException(errors);
        }

        private void RequireInitialized()
        {
            if (Root._initialization != 2)
                throw new InvalidOperationException("The tree has not been initialized successfully.");
        }
    }
}

