using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackThunder.UniEngine.StateMachines.Usm;
using BlackThunder.UniEngine.StateMachines.Usm.Core;
using BlackThunder.UniTest;
using NUnit.Framework;
using Parallel = BlackThunder.UniEngine.StateMachines.Usm.Parallel;

namespace BlackThunder.UniEngine.Tests
{
    [Category("Usm")]
    public class UsmTtcStateProjectTests
    {
        private static readonly string[] _path =
        {
            "Stop", "Observe", "Success", "Failure", "Reject", "Replay", "Observe",
            "Fault", "Success", "Stop", "Stop", "Reject", "Replay", "Fault", "Failure", "Observe"
        };

        private class TtcModel : Model
        {
            public Node Root => (Node)Subject;
            public UsmTestWork Parent { get; set; }
            public UsmTestWork SequenceWork { get; set; }
            public UsmTestWork SelectorWork { get; set; }
            public bool Active { get; set; }
            public int ParentStarts { get; set; }
            public int ParentUpdates { get; set; }
            public int ParentStops { get; set; }
            public int SequenceCalls { get; set; }
            public int SelectorCalls { get; set; }
        }

        private class TtcProject : CompactProject<TtcModel>
        {
            private readonly bool _continuous;


            public TtcProject(bool continuous) : base(ExecutionMode.Inline) => _continuous = continuous;

            public override IEnumerable<CompactLab<TtcModel>> CreateCompactLabs(TtcModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return Checked("Create", Create);
                    yield break;
                }

                if (!_continuous && model.ExecutionCount > _path.Length) yield break;
                foreach (var operation in Operations())
                {
                    if (_continuous || operation.Id == _path[model.ExecutionCount - 1]) yield return operation;
                }
            }

            private CompactLab<TtcModel> Checked(string id, Action<TtcModel> action) =>
                new(id, actor: action, asserter: AssertState);

            private IEnumerable<CompactLab<TtcModel>> Operations()
            {
                yield return Checked("Success", model => Tick(model, "success"));
                yield return Checked("Failure", model => Tick(model, "failure"));
                yield return Checked("Reject", model => Tick(model, "reject"));
                yield return Checked("Replay", model => Tick(model, "success", true));
                yield return Checked("Fault", model => Tick(model, "fault"));
                yield return Checked("Stop", model =>
                {
                    if (model.Active) model.ParentStops++;
                    model.Active = false;
                    model.Root.Stop(new FrameContext());
                });
                yield return Checked("Observe", model =>
                {
                    Assert.That(model.Root.Children.Count, Is.EqualTo(1));
                    Assert.That(model.Root.CheckCondition(null), Is.True);
                });
            }

            private void Create(TtcModel model)
            {
                model.Parent = new UsmTestWork();
                model.SequenceWork = new UsmTestWork
                {
                    Result = input => Equals(input, "fault")
                        ? throw new ApplicationException("sequence failure")
                        : new OperationInfo(DetailedOperationStatus.Success)
                };
                model.SelectorWork = new UsmTestWork
                {
                    Condition = input => !Equals(input, "reject"),
                    Result = input => new OperationInfo(Equals(input, "failure")
                        ? DetailedOperationStatus.Failure : DetailedOperationStatus.Success)
                };
                var matrix = new DecisionMatrixBuilder().FromEntry().GoTo("parallel").Build();
                model.Subject = UsmTests.Ready(new Node("root", model.Parent, new MatrixTransitioner(matrix),
                    new[]
                    {
                        new Node("parallel", transitioner: new Parallel(), children: new[]
                        {
                            new Node("sequence", transitioner: new Sequence(LoopType.Conditional), children: new[]
                            {
                                new Node("step", model.SequenceWork)
                            }),
                            new Node("selector", transitioner: new Selector(LoopType.Forced), children: new[]
                            {
                                new Node("choice", model.SelectorWork, abortPolicies: AbortPolicies.StopOnFailure)
                            })
                        })
                    }));
            }

            private void Tick(TtcModel model, string input, bool replay = false)
            {
                if (!model.Active) model.ParentStarts++;
                model.Active = input != "fault";
                var iterations = replay ? 2 : 1;
                model.ParentUpdates += iterations;
                model.SequenceCalls += iterations;
                if (input != "fault" && input != "reject") model.SelectorCalls += iterations;
                if (input == "fault") model.ParentStops++;
                if (replay) model.Root.ReframeOption = ReframeOption.Update;
                var frame = new FrameContext(input);

                if (input == "fault")
                    Assert.Throws<ApplicationException>(() => model.Root.Update(frame));
                else
                {
                    var result = model.Root.Update(frame);
                    Assert.That(result.IsRunning, Is.True);
                    var snapshot = (IReadOnlyList<OperationInfo>)result.Result;
                    Assert.That(snapshot.Count, Is.EqualTo(2));
                    Assert.That(snapshot[0].IsRunning && snapshot[1].IsRunning, Is.True);
                    Assert.That(frame.UpdateCount, Is.EqualTo((input == "reject" ? 5 : 6) * iterations));
                }
            }

            private void AssertState(TtcModel model)
            {
                Assert.That(model.Root.IsActive, Is.EqualTo(model.Active));
                Assert.That(model.Root.ReframeOption, Is.EqualTo(ReframeOption.None));
                Assert.That(model.Parent.Starts, Is.EqualTo(model.ParentStarts));
                Assert.That(model.Parent.Updates, Is.EqualTo(model.ParentUpdates));
                Assert.That(model.Parent.Stops, Is.EqualTo(model.ParentStops));
                Assert.That(model.Parent.Starts - model.Parent.Stops, Is.EqualTo(model.Active ? 1 : 0));
                Assert.That(model.SequenceWork.Starts, Is.EqualTo(model.SequenceCalls));
                Assert.That(model.SequenceWork.Updates, Is.EqualTo(model.SequenceCalls));
                Assert.That(model.SequenceWork.Stops, Is.EqualTo(model.SequenceCalls));
                Assert.That(model.SelectorWork.Starts, Is.EqualTo(model.SelectorCalls));
                Assert.That(model.SelectorWork.Updates, Is.EqualTo(model.SelectorCalls));
                Assert.That(model.SelectorWork.Stops, Is.EqualTo(model.SelectorCalls));
            }
        }


        [Test]
        public async Task FixedMixedTreeLifetimes()
        {
            var result = await new TtcProject(false).Execute(_path.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_path.Length + 1));
        }

        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task RandomContinuousMixedTreeLifetimes(int seed) =>
            await RandomContinuousTest.RunAsync(new TtcProject(true), seed);
    }
}

