using System;
using System.Collections.Generic;
using NUnit.Framework;
using BlackThunder.UniEngine.Infrastructure;
using BlackThunder.UniEngine.StateMachines.Scp;

namespace BlackThunder.UniEngine.Tests.StateMachines.Scp
{
    [Category("Scp")]
    public class ScpLifecycleTests
    {
        [Test]
        public void Sequence_ExecutesSuccessorsAndReportsCompletion()
        {
            var events = new List<string>();
            var first = new Clip("first").OnUpdated((clip, _) => { events.Add("first"); clip.Stop("value"); });
            var next = new Clip("next").OnUpdated((clip, _) => { events.Add("next"); clip.Stop(); });
            var sequence = new Sequence().Add(first).AddAfter(first, next);
            bool? completed = null;
            sequence.Stopped += success => completed = success;

            sequence.Update(0.5f);
            sequence.Update(0.5f);
            Assert.That(sequence.Update(0.5f, out var succeeded), Is.False);

            Assert.That(succeeded, Is.True);
            Assert.That(completed, Is.True);
            Assert.That(events, Is.EqualTo(new[] { "first", "next" }));
        }

        [Test]
        public void Sequence_StopIsIdempotentAndDisallowsMutationWhileActive()
        {
            var clip = new Clip();
            var stops = 0;
            var sequence = new Sequence(stopped: _ => stops++).Add(clip);
            sequence.Start();
            Assert.Throws<InvalidOperationException>(() => sequence.Add(new Clip()));
            sequence.Stop();
            sequence.Stop();

            Assert.That(clip.IsPlaying, Is.False);
            Assert.That(stops, Is.EqualTo(1));
            Assert.That(sequence.Start(), Is.True);
            sequence.Stop();
        }

        [Test]
        public void Sequence_FailingClipStopsOthersAndReportsException()
        {
            var failure = new InvalidOperationException("clip failure");
            Exception reported = null;
            ExceptionHandler.SetHandler(error => reported = error);
            var clip = new Clip().OnUpdated((_, _) => throw failure);
            var other = new Clip();
            var sequence = new Sequence().Add(clip).Add(other);
            try
            {
                Assert.That(sequence.Update(0.1f, out var succeeded), Is.False);
                Assert.That(succeeded, Is.False);
                Assert.That(reported, Is.SameAs(failure));
                Assert.That(clip.IsPlaying, Is.False);
                Assert.That(other.IsPlaying, Is.False);
            }
            finally
            {
                ExceptionHandler.SetHandler(null);
            }
        }
    }
}
