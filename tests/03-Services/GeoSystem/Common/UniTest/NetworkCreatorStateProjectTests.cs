using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.UniTest;
using NUnit.Framework;
using PlanarNode = BlackThunder.GeoSystem.Geometry.Planar.Node;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("NetworkCreators")]
    public class NetworkCreatorStateProjectTests
    {
        private static readonly string[] _path =
        {
            "Confirm", "Begin", "Free", "Confirm", "Free", "RejectSegment", "Confirm", "Allow",
            "Confirm", "Select", "Confirm", "Confirm", "Cancel", "Confirm", "Finish", "Begin",
            "Free", "RejectNode", "Confirm", "Allow", "Confirm", "Cancel", "Cancel", "Begin", "Finish",
        };

        private class Scene : IDisposable
        {
            public NetworkCreatorScene Input { get; } = new();
            public PlanarNode Existing { get; } = new(new Vector2(-10, 0));

            public void Dispose()
            {
                Input.Dispose();
                Existing.Dispose();
            }
        }

        private class CreatorModel : Model
        {
            public Scene Scene => (Scene)Subject;
            public bool Active { get; set; }
            public bool Selected { get; set; }
            public bool RejectNode { get; set; }
            public bool RejectSegment { get; set; }
            public int Position { get; set; }
            public INode<Vector2> Anchor { get; set; }
            public List<INode<Vector2>> CreatedNodes { get; } = new();
            public List<NetworkCreation<Vector2>> Links { get; } = new();
        }

        private class CreatorProject : CompactProject<CreatorModel>
        {
            private readonly bool _continuous;

            public CreatorProject(bool continuous = false) : base(ExecutionMode.Inline) => _continuous = continuous;

            public override IEnumerable<CompactLab<CreatorModel>> CreateCompactLabs(CreatorModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return Checked("Create", state => state.Subject = new Scene());
                    yield break;
                }
                if (!_continuous && model.ExecutionCount > _path.Length) yield break;
                foreach (var lab in Operations())
                    if (_continuous || lab.Id == _path[model.ExecutionCount - 1]) yield return lab;
            }

            private IEnumerable<CompactLab<CreatorModel>> Operations()
            {
                yield return Checked("Begin", state =>
                {
                    state.Scene.Input.Creator.Begin();
                    state.Active = true;
                });
                yield return Checked("Cancel", state =>
                {
                    state.Scene.Input.Creator.Cancel();
                    if (state.Anchor == null) state.Active = false;
                    state.Anchor = null;
                });
                yield return Checked("Finish", state =>
                {
                    state.Scene.Input.Creator.Finish();
                    state.Active = false;
                    state.Anchor = null;
                });
                yield return Checked("Free", state =>
                {
                    state.Selected = false;
                    state.Position++;
                    state.Scene.Input.Detection = new Detection<Vector2>(new Vector2(state.Position, 0));
                    state.Scene.Input.Creator.Update();
                });
                yield return Checked("Select", state =>
                {
                    state.Selected = true;
                    var node = state.Scene.Existing;
                    state.Scene.Input.Detection = new Detection<Vector2>(node.Position, node);
                    state.Scene.Input.Creator.Update();
                });
                yield return Checked("RejectNode", state =>
                    state.Scene.Input.RejectNode = state.RejectNode = true);
                yield return Checked("RejectSegment", state =>
                    state.Scene.Input.RejectSegment = state.RejectSegment = true);
                yield return Checked("Allow", state =>
                {
                    state.Scene.Input.RejectNode = state.RejectNode = false;
                    state.Scene.Input.RejectSegment = state.RejectSegment = false;
                });
                yield return Checked("Confirm", state =>
                {
                    var previous = state.Anchor;
                    var expected = state.Active && (state.Selected || !state.RejectNode)
                        && (!state.Selected || !ReferenceEquals(previous, state.Scene.Existing))
                        && (previous == null || !state.RejectSegment);
                    var success = state.Scene.Input.Creator.TryConfirm(out var result);
                    Assert.That(success, Is.EqualTo(expected));
                    if (!expected)
                    {
                        Assert.That(result.Node, Is.Null);
                        Assert.That(result.Segment, Is.Null);
                        return;
                    }

                    Assert.That(result.IsNewNode, Is.EqualTo(!state.Selected));
                    if (state.Selected) Assert.That(result.Node, Is.SameAs(state.Scene.Existing));
                    else
                    {
                        Assert.That(result.Node.Position, Is.EqualTo(new Vector2(state.Position, 0)));
                        state.CreatedNodes.Add(result.Node);
                    }

                    if (previous == null) Assert.That(result.Segment, Is.Null);
                    else
                    {
                        Assert.That(result.Segment.From, Is.SameAs(previous));
                        Assert.That(result.Segment.To, Is.SameAs(result.Node));
                        state.Links.Add(result);
                    }

                    state.Anchor = result.Node;
                });
            }

            private CompactLab<CreatorModel> Checked(string id, Action<CreatorModel> action) =>
                new(id, state =>
                {
                    action(state);
                    var input = state.Scene.Input;
                    var creator = input.Creator;
                    Assert.That(creator.IsCreating, Is.EqualTo(state.Active));
                    Assert.That(creator.AnchorNode, Is.SameAs(state.Anchor));
                    var expected = !state.Active ? LinearNetworkCreatorState.Idle
                        : state.Anchor == null
                            ? state.Selected
                                ? LinearNetworkCreatorState.SelectFirstNode : LinearNetworkCreatorState.PutFirstNode
                            : state.Selected
                                ? LinearNetworkCreatorState.SelectNextNode : LinearNetworkCreatorState.PutNextNode;
                    Assert.That(creator.State, Is.EqualTo(expected));
                    Assert.That(input.Nodes.Where(node => !node.IsDisposed), Is.EqualTo(state.CreatedNodes));
                    Assert.That(input.Segments.Where(segment => !segment.IsDisposed),
                        Is.EqualTo(state.Links.Select(link => link.Segment)));
                    foreach (var link in state.Links)
                    {
                        Assert.That(link.Segment.From.Segments.Contains(link.Segment), Is.True);
                        Assert.That(link.Segment.To.Segments.Contains(link.Segment), Is.True);
                    }
                    Assert.That(state.Scene.Existing.IsDisposed, Is.False);
                });
        }

        [Test]
        public async Task FixedCreatorTransitions()
        {
            // Creator plan: deterministic traversal covers placement, selection, cancellation, and failed retries.
            var result = await new CreatorProject().Execute(_path.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_path.Length + 1));
        }

        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public Task RandomCreatorTransitions(int seed) =>
            // Creator plan: independent seeds accumulate 100 operations and verify graph ownership after each step.
            RandomContinuousTest.RunAsync(new CreatorProject(continuous: true), seed);
    }
}
