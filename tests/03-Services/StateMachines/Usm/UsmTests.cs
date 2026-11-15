using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.UniEngine.StateMachines;
using BlackThunder.UniEngine.StateMachines.Usm;
using BlackThunder.UniEngine.StateMachines.Usm.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture]
    [Category("Usm")]
    public class UsmTests
    {
        private class ProbeTransitioner : ITransitioner
        {
            public Action Starting;
            public Action Stopping;
            public int Updates;
            public INode[] Children;

            public void Initialize(object owner, object blackboard, IEnumerable<INode> children) =>
                Children = children.ToArray();
            public void Start(object input, IFrameContext context) => Starting?.Invoke();
            public OperationInfo Update(IFrameContext context) { Updates++; return new OperationInfo(DetailedOperationStatus.Running); }
            public void Stop(DetailedOperationStatus reason, IFrameContext context) => Stopping?.Invoke();
        }

        private class EmptyName
        {
            public override string ToString() => " ";
        }


        [Test]
        public void CommonNamesAcceptStringsTypesAndObjects()
        {
            Assert.That(StateMachineNames.GetName("idle"), Is.EqualTo("idle"));
            Assert.That(StateMachineNames.GetName(typeof(UsmTests)), Is.EqualTo(nameof(UsmTests)));
            Assert.That(StateMachineNames.GetName(42), Is.EqualTo("42"));
            Assert.Throws<ArgumentNullException>(() => StateMachineNames.GetName(null));
            Assert.Throws<ArgumentException>(() => StateMachineNames.GetName(" "));
            Assert.Throws<ArgumentException>(() => StateMachineNames.GetName(new EmptyName()));
        }

        [Test]
        public void RootInitializationReachesEveryWorkAndCannotBeRepeated()
        {
            var parent = new UsmTestWork();
            var child = new UsmTestWork();
            var node = new Node(
                "root",
                work: parent,
                transitioner: new Sequence(),
                children: new[]
                {
                    new Node("child", work: child)
                });
            Assert.Throws<InvalidOperationException>(() => node.Update(new FrameContext()));
            Assert.Throws<InvalidOperationException>(() => node.CheckCondition(null));
            var owner = new object();
            var blackboard = new object();
            node.Initialize(owner, blackboard);
            Assert.That(parent.Owner, Is.SameAs(owner));
            Assert.That(child.Owner, Is.SameAs(owner));
            Assert.That(parent.Blackboard, Is.SameAs(blackboard));
            Assert.That(child.Blackboard, Is.SameAs(blackboard));
            Assert.Throws<InvalidOperationException>(() => node.Initialize());
        }

        [Test]
        public void FailedInitializationPermanentlyRejectsExecutionAndRetry()
        {
            var work = new UsmTestWork { Initializing = () => throw new ApplicationException("init") };
            var root = new Node("root", work: work);
            Assert.Throws<ApplicationException>(() => root.Initialize());
            work.Initializing = null;
            Assert.Throws<InvalidOperationException>(() => root.Initialize());
            Assert.Throws<InvalidOperationException>(() => root.Update(new FrameContext()));
            Assert.That(work.Starts, Is.Zero);
        }

        [Test]
        public void DefaultLeafPreservesPayloadAndRecordsExecution()
        {
            var node = Ready(new Node("leaf"));
            var payload = new object();
            var frame = new FrameContext(payload);
            Assert.That(node.Update(frame).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.Running));
            Assert.That(frame.Payload, Is.SameAs(payload));
            Assert.That(frame.Id, Is.EqualTo("leaf"));
            Assert.That(frame.Reports.Last().OperationInfo.DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.Running));
            Assert.That(frame.UpdateCount, Is.EqualTo(1));
        }

        [Test]
        public void RunningCompletionAndRestartHaveBalancedLifetimes()
        {
            var work = new UsmTestWork();
            var node = Ready(new Node("leaf", work: work));
            node.Update(new FrameContext());
            node.Update(new FrameContext());
            Assert.That(work.Starts, Is.EqualTo(1));
            work.Result = _ => new OperationInfo(DetailedOperationStatus.Success);
            Assert.That(node.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.Success));
            node.Stop(new FrameContext());
            Assert.That(work.Stops, Is.EqualTo(1));
            Assert.That(node.IsActive, Is.False);
            node.Update(new FrameContext());
            Assert.That(work.Starts, Is.EqualTo(2));
            Assert.That(work.Stops, Is.EqualTo(2));
        }

        [Test]
        public void TerminalWorkSkipsTransitionerAndStopsPreviouslyActiveChild()
        {
            var parent = new UsmTestWork();
            var child = new UsmTestWork();
            var root = Ready(new Node(
                "root",
                work: parent,
                transitioner: new Sequence(),
                children: new[]
                {
                    new Node("child", work: child)
                }));
            root.Update(new FrameContext());
            parent.Result = _ => new OperationInfo(DetailedOperationStatus.Success);
            root.Update(new FrameContext());
            Assert.That(child.Updates, Is.EqualTo(1));
            Assert.That(child.Stops, Is.EqualTo(1));
            Assert.That(child.StopReason, Is.EqualTo(DetailedOperationStatus.HaltedByParent));
        }

        [Test]
        public void StopRunsFromDescendantsToParentExactlyOnce()
        {
            var events = new List<string>();
            var root = Ready(new Node(
                "root",
                work: new UsmTestWork("root", events),
                transitioner: new Sequence(),
                children: new[]
                {
                    new Node(
                        "child",
                        work: new UsmTestWork("child", events),
                        transitioner: new Selector(),
                        children: new[]
                        {
                            new Node("leaf", work: new UsmTestWork("leaf", events))
                        })
                }));
            root.Update(new FrameContext());
            events.Clear();
            root.Stop(new FrameContext());
            root.Stop(new FrameContext());
            Assert.That(events, Is.EqualTo(new[]
            {
                "leaf:stop:HaltedByParent", "child:stop:HaltedByParent", "root:stop:HaltedByExternal"
            }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void StopInsideWorkCallbackPreventsFurtherExecution(bool stopOnStart)
        {
            var work = new UsmTestWork();
            var transitioner = new ProbeTransitioner();
            var node = Ready(new Node("node", work: work, transitioner: transitioner));
            Action stop = () => node.Stop(new FrameContext());
            if (stopOnStart) work.Starting = stop;
            else work.Updating = stop;
            Assert.That(node.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.HaltedByExternal));
            Assert.That(work.Updates, Is.EqualTo(stopOnStart ? 0 : 1));
            Assert.That(work.Stops, Is.EqualTo(1));
            Assert.That(transitioner.Updates, Is.Zero);
        }

        [Test]
        public void StopInsideTransitionerStartPreventsItsUpdate()
        {
            var transitioner = new ProbeTransitioner();
            var node = Ready(new Node("node", transitioner: transitioner));
            transitioner.Starting = () => node.Stop(new FrameContext());
            Assert.That(node.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.HaltedByExternal));
            Assert.That(transitioner.Updates, Is.Zero);
        }

        [Test]
        public void ParentStopInsideChildUpdatePreventsFallbackChildFromStarting()
        {
            var child = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Failure) };
            var fallback = new UsmTestWork();
            var root = Ready(new Node(
                "root",
                transitioner: new Selector(),
                children: new[]
                {
                    new Node("child", work: child),
                    new Node("fallback", work: fallback)
                }));
            child.Updating = () => root.Stop(new FrameContext());
            Assert.That(root.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.HaltedByExternal));
            Assert.That(fallback.Starts, Is.Zero);
            Assert.That(child.Stops, Is.EqualTo(1));
        }

        [Test]
        public void UpdateReentryThrowsAndCleansUpActiveTree()
        {
            var work = new UsmTestWork();
            var node = Ready(new Node("node", work: work));
            work.Updating = () => node.Update(new FrameContext());
            Assert.Throws<InvalidOperationException>(() => node.Update(new FrameContext()));
            Assert.That(node.IsActive, Is.False);
            Assert.That(work.StopReason, Is.EqualTo(DetailedOperationStatus.Failure));
            work.Updating = null;
            Assert.DoesNotThrow(() => node.Update(new FrameContext()));
        }

        [Test]
        public void ExecutionAndAllCleanupFailuresArePreserved()
        {
            var runError = new ApplicationException("run");
            var childError = new ApplicationException("child stop");
            var parentError = new ApplicationException("parent stop");
            var child = new UsmTestWork { Updating = () => throw runError, Stopping = () => throw childError };
            var parent = new UsmTestWork { Stopping = () => throw parentError };
            var root = Ready(new Node(
                "root",
                work: parent,
                transitioner: new Sequence(),
                children: new[]
                {
                    new Node("child", work: child)
                }));
            var error = Assert.Throws<AggregateException>(() => root.Update(new FrameContext()));
            Assert.That(error.Flatten().InnerExceptions, Is.EquivalentTo(new[] { runError, childError, parentError }));
            Assert.That(root.IsActive, Is.False);
            Assert.That(child.Stops, Is.EqualTo(1));
            Assert.That(parent.Stops, Is.EqualTo(1));
            Assert.DoesNotThrow(() => root.Stop(new FrameContext()));
        }

        [Test]
        public void StopStillCleansUpWorkWhenTransitionerStopThrows()
        {
            var work = new UsmTestWork();
            var error = new ApplicationException("transitioner stop");
            var transitioner = new ProbeTransitioner { Stopping = () => throw error };
            var root = Ready(new Node("root", work: work, transitioner: transitioner));
            root.Update(new FrameContext());
            Assert.That(Assert.Throws<ApplicationException>(() => root.Stop(new FrameContext())), Is.SameAs(error));
            Assert.That(work.Stops, Is.EqualTo(1));
            Assert.That(root.IsActive, Is.False);
        }

        [Test]
        public void ChildCannotInitializeSeparatelyAndInvalidStopReasonPreservesRunningState()
        {
            var transitioner = new ProbeTransitioner();
            var root = Ready(new Node("root", transitioner: transitioner, children: new[] { new Node("child") }));
            Assert.Throws<InvalidOperationException>(() => transitioner.Children[0].Initialize());
            root.Update(new FrameContext());
            Assert.Throws<ArgumentException>(() => root.Stop(new FrameContext(), DetailedOperationStatus.Running));
            Assert.That(root.IsActive, Is.True);
            Assert.Throws<ArgumentNullException>(() => root.Update(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FrameContext(maxNodeUpdates: 0));
        }

        [Test]
        public void InactiveParentAndStopCallbackCannotRestartAChild()
        {
            var transitioner = new ProbeTransitioner();
            var child = new UsmTestWork();
            var root = Ready(new Node("root", transitioner: transitioner, children: new[] { new Node("child", work: child) }));
            var childNode = transitioner.Children[0];
            Assert.Throws<InvalidOperationException>(() => childNode.Update(new FrameContext()));
            root.Update(new FrameContext());
            childNode.Update(new FrameContext());
            child.Stopping = () =>
                Assert.Throws<InvalidOperationException>(() => childNode.Update(new FrameContext()));
            root.Stop(new FrameContext());
            Assert.That(childNode.IsActive, Is.False);
            Assert.That(child.Starts, Is.EqualTo(1));
        }

        [Test]
        public void StoppingABranchRejectsUpdatesInItsStillActiveDescendants()
        {
            var rootTransition = new ProbeTransitioner();
            var branchTransition = new ProbeTransitioner();
            var laterTransition = new ProbeTransitioner();
            var firstWork = new UsmTestWork();
            var leafWork = new UsmTestWork();
            var root = Ready(new Node(
                "root",
                transitioner: rootTransition,
                children: new[]
                {
                    new Node(
                        "branch",
                        transitioner: branchTransition,
                        children: new[]
                        {
                            new Node("first", work: firstWork),
                            new Node(
                                "later",
                                transitioner: laterTransition,
                                children: new[]
                                {
                                    new Node("leaf", work: leafWork)
                                })
                        })
                }));
            root.Update(new FrameContext());
            var branch = rootTransition.Children[0];
            branch.Update(new FrameContext());
            foreach (var child in branchTransition.Children) child.Update(new FrameContext());
            var leaf = laterTransition.Children[0];
            leaf.Update(new FrameContext());
            firstWork.Stopping = () => Assert.Throws<InvalidOperationException>(() => leaf.Update(new FrameContext()));
            branch.Stop(new FrameContext());
            Assert.That(root.IsActive, Is.True);
            Assert.That(leaf.IsActive, Is.False);
            Assert.That(leafWork.Updates, Is.EqualTo(1));
        }

        internal static Node Ready(Node node)
        {
            node.Initialize();
            return node;
        }
    }
}
