using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackThunder.UniEngine.Infrastructure;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    internal static class ExceptionHandlerContinuousScenario
    {
        // Internal
        private const string UseFirstHandler = "UseFirstHandler";
        private const string UseReplacementHandler = "UseReplacementHandler";
        private const string UseThrowingHandler = "UseThrowingHandler";
        private const string ClearHandler = "ClearHandler";
        private const string Report = "Report";
        private const string ReportNull = "ReportNull";
        private const string SafeInvoke = "SafeInvoke";

        private static readonly string[] _candidates =
        {
            UseFirstHandler, UseReplacementHandler, UseThrowingHandler, ClearHandler, Report, ReportNull, SafeInvoke,
        };


        // Content
        public static Task RunAsync(int seed) =>
            RandomContinuousTest.RunAsync(new InfrastructureContinuousProject(_ => _candidates, VerifyHistory), seed);

        private static void VerifyHistory(string[] history)
        {
            var actualCalls = new List<string>();
            var policy = ClearHandler;
            var handlerFailure = new InvalidOperationException("Handler failure");
            ApplicationException reported = null;
            ExceptionHandler.SetHandler(null);

            foreach (var step in history)
            {
                actualCalls.Clear();
                reported = new ApplicationException("Reported failure");

                switch (step)
                {
                    case UseFirstHandler:
                    case UseReplacementHandler:
                    case UseThrowingHandler:
                        policy = step;
                        ExceptionHandler.SetHandler(exception =>
                        {
                            Assert.That(exception, Is.SameAs(reported));
                            actualCalls.Add(step);

                            if (step == UseThrowingHandler)
                                throw handlerFailure;
                        });

                        CheckReport();
                        break;
                    case ClearHandler:
                        policy = ClearHandler;
                        ExceptionHandler.SetHandler(null);
                        CheckReport();
                        break;
                    case Report:
                        CheckReport();
                        break;
                    case ReportNull:
                        Assert.Throws<ArgumentNullException>(() => ExceptionHandler.Report(null));
                        Assert.That(actualCalls, Is.Empty);
                        break;
                    case SafeInvoke:
                        Action subscribers = () =>
                        {
                            actualCalls.Add("failing subscriber");
                            throw reported;
                        };

                        subscribers += () => actualCalls.Add("following subscriber");
                        var expectedCalls = new List<string> { "failing subscriber" };
                        InvokeAndCheckException(() => subscribers.SafeInvoke());

                        if (policy != ClearHandler)
                            expectedCalls.Add(policy);

                        if (ExpectedException() == null)
                            expectedCalls.Add("following subscriber");

                        Assert.That(actualCalls, Is.EqualTo(expectedCalls));
                        break;
                    default:
                        throw new ArgumentException("Unknown ExceptionHandler step: " + step, nameof(history));
                }
            }

            void CheckReport()
            {
                InvokeAndCheckException(() => ExceptionHandler.Report(reported));
                var expectedCalls = policy == ClearHandler ? Array.Empty<string>() : new[] { policy };
                Assert.That(actualCalls, Is.EqualTo(expectedCalls));
            }

            Exception ExpectedException() => policy switch
            {
                ClearHandler => reported,
                UseThrowingHandler => handlerFailure,
                _ => null,
            };

            void InvokeAndCheckException(Action invoke)
            {
                var expected = ExpectedException();

                if (expected == null)
                    invoke();
                else
                    Assert.That(Assert.Catch<Exception>(() => invoke()), Is.SameAs(expected));
            }
        }
    }
}
