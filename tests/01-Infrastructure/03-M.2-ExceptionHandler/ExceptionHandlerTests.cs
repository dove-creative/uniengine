using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Infrastructure;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class ExceptionHandlerTests
    {
        // Content
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        [Test]
        public void ReportRejectsNullBeforeInvokingTheHandler()
        {
            var calls = 0;
            ExceptionHandler.SetHandler(_ => calls++);

            var failure = Assert.Throws<ArgumentNullException>(() => ExceptionHandler.Report(null));

            Assert.That(failure.ParamName, Is.EqualTo("exception"));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void ReportPassesTheOriginalExceptionToTheHandler()
        {
            var failure = new ApplicationException("original");
            Exception observed = null;
            ExceptionHandler.SetHandler(exception => observed = exception);

            Assert.DoesNotThrow(() => ExceptionHandler.Report(failure));

            Assert.That(observed, Is.SameAs(failure));
        }

        [Test]
        public void UnhandledReportPreservesTheExceptionAndOriginalThrowSite()
        {
            var failure = CaptureFailure();

            var observed = Assert.Throws<ApplicationException>(() => ExceptionHandler.Report(failure));

            Assert.That(observed, Is.SameAs(failure));
            Assert.That(observed.StackTrace, Does.Contain(nameof(ThrowOriginalFailure)));
        }

        [Test]
        public void ReportPropagatesTheHandlerFailure()
        {
            var original = new ApplicationException("original");
            var handlerFailure = new InvalidOperationException("handler");
            Exception observed = null;
            ExceptionHandler.SetHandler(exception =>
            {
                observed = exception;
                throw handlerFailure;
            });

            var propagated = Assert.Throws<InvalidOperationException>(() => ExceptionHandler.Report(original));

            Assert.That(observed, Is.SameAs(original));
            Assert.That(propagated, Is.SameAs(handlerFailure));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void NullSafeInvokeDoesNotReportAnything(int arity)
        {
            var reports = 0;
            ExceptionHandler.SetHandler(_ => reports++);

            Assert.DoesNotThrow(() => InvokeNull(arity));

            Assert.That(reports, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SafeInvokePreservesMulticastOrderAndArguments(int arity)
        {
            var calls = new List<string>();
            var invoke = CreateInvocation(arity, calls, null);

            invoke();

            Assert.That(calls, Is.EqualTo(new[] { "before", "middle", "after" }));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void HandledSafeInvokeContinuesAndRetainsTheFailingSubscriber(int arity)
        {
            var failure = new ApplicationException("subscriber");
            var calls = new List<string>();
            var reports = new List<Exception>();
            var invoke = CreateInvocation(arity, calls, failure);
            ExceptionHandler.SetHandler(reports.Add);

            invoke();
            invoke();

            Assert.That(calls, Is.EqualTo(new[] { "before", "middle", "after", "before", "middle", "after" }));
            Assert.That(reports.Count, Is.EqualTo(2));
            Assert.That(reports[0], Is.SameAs(failure));
            Assert.That(reports[1], Is.SameAs(failure));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void UnhandledSafeInvokeStopsAtTheFailingSubscriber(int arity)
        {
            var failure = new ApplicationException("subscriber");
            var calls = new List<string>();
            var invoke = CreateInvocation(arity, calls, failure);

            var observed = Assert.Throws<ApplicationException>(() => invoke());

            Assert.That(observed, Is.SameAs(failure));
            Assert.That(calls, Is.EqualTo(new[] { "before", "middle" }));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SafeInvokeStopsAndPropagatesWhenTheReportHandlerThrows(int arity)
        {
            var failure = new ApplicationException("subscriber");
            var handlerFailure = new InvalidOperationException("handler");
            var calls = new List<string>();
            var invoke = CreateInvocation(arity, calls, failure);
            Exception observed = null;
            ExceptionHandler.SetHandler(exception =>
            {
                observed = exception;
                throw handlerFailure;
            });

            var propagated = Assert.Throws<InvalidOperationException>(() => invoke());

            Assert.That(observed, Is.SameAs(failure));
            Assert.That(propagated, Is.SameAs(handlerFailure));
            Assert.That(calls, Is.EqualTo(new[] { "before", "middle" }));
        }

        private static void InvokeNull(int arity)
        {
            switch (arity)
            {
                case 0: ((Action)null).SafeInvoke(); break;
                case 1: ((Action<int>)null).SafeInvoke(17); break;
                case 2: ((Action<int, string>)null).SafeInvoke(17, "payload"); break;
                case 3: ((Action<int, string, object>)null).SafeInvoke(17, "payload", new object()); break;
                case 4: ((Action<int, string, object, bool>)null).SafeInvoke(17, "payload", new object(), true); break;
                default: throw new ArgumentOutOfRangeException(nameof(arity));
            }
        }

        private static Action CreateInvocation(int arity, List<string> calls, Exception failure)
        {
            var marker = new object();
            var expected = new object[] { 17, "payload", marker, true }.Take(arity).ToArray();

            void Record(string name, params object[] arguments)
            {
                Assert.That(arguments, Is.EqualTo(expected), "Arguments must reach every multicast subscriber.");
                calls.Add(name);
                if (name == "middle" && failure != null)
                    throw failure;
            }

            switch (arity)
            {
                case 0:
                    Action action = () => Record("before");
                    action += () => Record("middle");
                    action += () => Record("after");
                    return () => action.SafeInvoke();
                case 1:
                    Action<int> action1 = value => Record("before", value);
                    action1 += value => Record("middle", value);
                    action1 += value => Record("after", value);
                    return () => action1.SafeInvoke(17);
                case 2:
                    Action<int, string> action2 = (value, text) => Record("before", value, text);
                    action2 += (value, text) => Record("middle", value, text);
                    action2 += (value, text) => Record("after", value, text);
                    return () => action2.SafeInvoke(17, "payload");
                case 3:
                    Action<int, string, object> action3 = (value, text, item) => Record("before", value, text, item);
                    action3 += (value, text, item) => Record("middle", value, text, item);
                    action3 += (value, text, item) => Record("after", value, text, item);
                    return () => action3.SafeInvoke(17, "payload", marker);
                case 4:
                    Action<int, string, object, bool> action4 = (value, text, item, flag) => Record("before", value, text, item, flag);
                    action4 += (value, text, item, flag) => Record("middle", value, text, item, flag);
                    action4 += (value, text, item, flag) => Record("after", value, text, item, flag);
                    return () => action4.SafeInvoke(17, "payload", marker, true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(arity));
            }
        }

        private static ApplicationException CaptureFailure()
        {
            try
            {
                ThrowOriginalFailure();
            }
            catch (ApplicationException exception)
            {
                return exception;
            }

            throw new InvalidOperationException("The failure source did not throw.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowOriginalFailure() => throw new ApplicationException("original");
    }
}
