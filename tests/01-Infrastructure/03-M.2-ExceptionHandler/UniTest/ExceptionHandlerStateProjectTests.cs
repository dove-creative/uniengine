using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackThunder.UniEngine.Infrastructure;
using BlackThunder.UniTest;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class ExceptionHandlerStateProjectTests
    {
        // Internal
        private class HandlerState
        {
            public ApplicationException Failure { get; } = new("scenario");
            public List<string> Calls { get; } = new();
        }

        private class HandlerProject : CompactProject<Model>
        {
            public HandlerProject() : base(ExecutionMode.Inline) { }

            public override IEnumerable<CompactLab<Model>> CreateCompactLabs(Model model)
            {
                switch (model.ExecutionCount)
                {
                    case 0:
                        return new[]
                        {
                            new CompactLab<Model>("RegisterHandler")
                            {
                                Actor = state =>
                                {
                                    var subject = new HandlerState();
                                    state.Subject = subject;

                                    ExceptionHandler.SetHandler(exception =>
                                    {
                                        Assert.That(exception, Is.SameAs(subject.Failure));
                                        subject.Calls.Add("first");
                                    });

                                    ExceptionHandler.Report(subject.Failure);
                                },
                                Asserter = state => Assert.That(
                                    ((HandlerState)state.Subject).Calls,
                                    Is.EqualTo(new[] { "first" })),
                            },
                        };
                    case 1:
                        return new[]
                        {
                            new CompactLab<Model>("ReplaceHandler")
                            {
                                Actor = state =>
                                {
                                    var subject = (HandlerState)state.Subject;

                                    ExceptionHandler.SetHandler(exception =>
                                    {
                                        Assert.That(exception, Is.SameAs(subject.Failure));
                                        subject.Calls.Add("replacement");
                                    });

                                    ExceptionHandler.Report(subject.Failure);
                                },
                                Asserter = state => Assert.That(
                                    ((HandlerState)state.Subject).Calls,
                                    Is.EqualTo(new[] { "first", "replacement" })),
                            },
                        };
                    case 2:
                        return new[]
                        {
                            new CompactLab<Model>("ClearHandler")
                            {
                                Actor = state =>
                                {
                                    var subject = (HandlerState)state.Subject;
                                    ExceptionHandler.SetHandler(null);
                                    var propagated = Assert.Throws<ApplicationException>(
                                        () => ExceptionHandler.Report(subject.Failure));
                                    Assert.That(propagated, Is.SameAs(subject.Failure));
                                },
                                Asserter = state => Assert.That(
                                    ((HandlerState)state.Subject).Calls,
                                    Is.EqualTo(new[] { "first", "replacement" })),
                            },
                        };
                    default:
                        return Array.Empty<CompactLab<Model>>();
                }
            }
        }


        // Content
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        [Test]
        public async Task RegisterReplaceAndClearChangeTheReportingPolicyThroughUniTest()
        {
            var project = new HandlerProject();
            var result = await project.Execute(3);

            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(3));
        }
    }
}
