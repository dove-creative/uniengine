using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackThunder.UniEngine.StateMachines.Usm;
using BlackThunder.UniEngine.StateMachines.Usm.Core;
using BlackThunder.UniTest;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [Category("Usm")]
    public class UsmStateProjectTests
    {
        private static readonly string[] _fixedPath =
        {
            "Observe", "Halt", "TickA", "TickA", "TickB", "Observe", "TickA", "Finish",
            "TickB", "Fault", "Observe", "TickA", "Halt", "Halt", "Finish", "Fault", "TickB", "Finish"
        };

        private class MachineModel : Model
        {
            public INode Root => (INode)Subject;
            public UsmTestWork[] Work { get; } = new UsmTestWork[3];
            public int[] Starts { get; } = new int[3];
            public int[] Updates { get; } = new int[3];
            public int[] Stops { get; } = new int[3];
            public int Current { get; set; } = -1;
        }

        private class MachineProject : CompactProject<MachineModel>
        {
            private readonly bool _continuous;

            public MachineProject(bool continuous) : base(ExecutionMode.Inline) => _continuous = continuous;

            public override IEnumerable<CompactLab<MachineModel>> CreateCompactLabs(MachineModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return Checked("Create", Create);
                    yield break;
                }

                if (!_continuous && model.ExecutionCount > _fixedPath.Length) yield break;

                foreach (var operation in Operations())
                {
                    if (_continuous || operation.Id == _fixedPath[model.ExecutionCount - 1]) yield return operation;
                }
            }

            private IEnumerable<CompactLab<MachineModel>> Operations()
            {
                yield return Checked("TickA", model => Tick(model, "a"));
                yield return Checked("TickB", model => Tick(model, "b"));
                yield return Checked("Finish", model => Tick(model, "finish"));
                yield return Checked("Fault", model => Tick(model, "fault"));
                yield return Checked("Halt", model =>
                {
                    if (model.Current >= 0)
                    {
                        model.Stops[0]++;
                        model.Stops[model.Current]++;
                        model.Current = -1;
                    }

                    model.Root.Stop(new FrameContext());
                });
                yield return Checked("Observe", model =>
                {
                    Assert.That(model.Root.CheckCondition(null), Is.True);
                    Assert.Throws<InvalidOperationException>(() => model.Root.Initialize());
                });
            }

            private CompactLab<MachineModel> Checked(string id, Action<MachineModel> actor) =>
                new(id, actor: actor, asserter: AssertState);

            private void Create(MachineModel model)
            {
                model.Work[0] = new UsmTestWork("root");

                for (var i = 1; i < 3; i++)
                {
                    model.Work[i] = new UsmTestWork(i == 1 ? "a" : "b")
                    {
                        Result = input => input switch
                        {
                            "finish" => new OperationInfo(DetailedOperationStatus.Success),
                            "fault" => throw new ApplicationException("work failure"),
                            _ => new OperationInfo(DetailedOperationStatus.Running),
                        }
                    };
                }

                var matrix = new DecisionMatrixBuilder().FromEntry().GoTo("a").With("b").GoTo("b")
                    .From("a").When(DetailedOperationStatus.Running).With("b").GoTo("b")
                    .From("b").When(DetailedOperationStatus.Running).With("a").GoTo("a").Build();
                model.Subject = UsmTests.Ready(new Node(
                    "root",
                    work: model.Work[0],
                    transitioner: new MatrixTransitioner(matrix),
                    children: new[]
                    {
                        new Node("a", work: model.Work[1]),
                        new Node("b", work: model.Work[2])
                    }));
            }

            private void Tick(MachineModel model, string input)
            {
                if (model.Current < 0)
                {
                    model.Current = input == "b" ? 2 : 1;
                    model.Starts[0]++;
                    model.Starts[model.Current]++;
                }

                model.Updates[0]++;
                model.Updates[model.Current]++;

                if (input == "finish" || input == "fault")
                {
                    model.Stops[0]++;
                    model.Stops[model.Current]++;
                    model.Current = -1;
                }
                else
                {
                    var next = input == "a" ? 1 : 2;

                    if (next != model.Current)
                    {
                        model.Stops[model.Current]++;
                        model.Current = next;
                        model.Starts[next]++;
                        model.Updates[next]++;
                    }
                }

                var frame = new FrameContext(input);

                if (input == "fault") Assert.Throws<ApplicationException>(() => model.Root.Update(frame));
                else
                {
                    var result = model.Root.Update(frame);
                    Assert.That(result.DetailedOperationStatus,
                        Is.EqualTo(input == "finish" ? DetailedOperationStatus.Success : DetailedOperationStatus.Running));
                    Assert.That(frame.Payload, Is.EqualTo(input));
                }
            }

            private void AssertState(MachineModel model)
            {
                Assert.That(model.Root.IsActive, Is.EqualTo(model.Current >= 0));

                for (var i = 0; i < 3; i++)
                {
                    Assert.That(model.Work[i].Starts, Is.EqualTo(model.Starts[i]), $"starts {i}");
                    Assert.That(model.Work[i].Updates, Is.EqualTo(model.Updates[i]), $"updates {i}");
                    Assert.That(model.Work[i].Stops, Is.EqualTo(model.Stops[i]), $"stops {i}");
                    var active = model.Current >= 0 && (i == 0 || i == model.Current);
                    Assert.That(model.Work[i].Starts - model.Work[i].Stops, Is.EqualTo(active ? 1 : 0));
                }
            }
        }


        [Test]
        public async Task FixedMachineLifetimes()
        {
            var project = new MachineProject(false);
            var result = await project.Execute(_fixedPath.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_fixedPath.Length + 1));
        }

        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task RandomContinuousMachineLifetimes(int seed) =>
            await RandomContinuousTest.RunAsync(new MachineProject(true), seed);
    }
}
