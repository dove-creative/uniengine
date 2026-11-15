using NUnit.Framework;
using BlackThunder.UniEngine.StateMachines.Scp;
using BlackThunder.UniEngine.Infrastructure;

namespace BlackThunder.UniEngine.Tests.StateMachines.Scp
{
    [Category("Scp")]
    public class ScpTests
    {
        [Test]
        public void Normal()
        {
            var i = 0;
            var executionCount = 0;
            var stopped = false;

            var sequence = new Sequence()
                .Add(new Clip("Clip1")
                    .OnStarted((self, _) => Log.Write($"{self.Name} Started"))
                    .OnUpdated((self, _) =>
                    {
                        Log.Write($"{self.Name} Updated - " + i);
                        i++;

                        if (i >= 10)
                            self.Stop();
                    })
                    .OnStopped(self =>
                    {
                        Log.Write($"{self.Name} Stopped");
                    })
                    .AssignTo(out var clip1)
                )
                .Add(
                    new CTC(clip1),
                    new Clip("Clip2")
                    .OnStarted((self, _) => Log.Write($"{self.Name} Started"))
                    .OnUpdated((self, _) =>
                    {
                        Log.Write($"{self.Name} Updated - " + i);
                        i++;

                        if (i >= 20)
                            self.Stop();
                    })
                    .OnStopped(self =>
                    {
                        Log.Write($"{self.Name} Stopped");
                    })
                    .AssignTo(out var clip2)
                )
                .Add(
                    new CTC(clip1),
                    new Clip("Clip3")
                    .OnStarted((self, _) => Log.Write($"{self.Name} Started"))
                    .OnUpdated((self, _) =>
                    {
                        Log.Write($"{self.Name} Updated - " + i);
                        i++;

                        if (i >= 20)
                            self.Stop();
                    })
                    .OnStopped(self =>
                    {
                        Log.Write($"{self.Name} Stopped");
                    })
                    .AssignTo(out var clip3)
                )
                .Add(
                    new PlayCondition(clip2, clip3),
                    new Clip("Clip4")
                    .OnStarted((self, _) =>
                    {
                        Log.Write($"{self.Name} Started");
                        self.Stop();
                    })
                    .OnStopped(self =>
                    {
                        Log.Write($"{self.Name} Stopped");
                        stopped = true;
                    })
                );


            while (!stopped)
            {
                executionCount++;
                if (executionCount >= 100)
                    Assert.Fail("Timeout");

                sequence.Update(0.1f);
            }
        }

        [Test]
        public void Recurse()
        {
            var i = 0;
            var executionCount = 0;
            var stopped = false;

            var sequence = new Sequence()
                .Add(
                    new Clip("Clip1")
                    .OnStarted((self, _) => Log.Write($"{self.Name} Started"))
                    .OnUpdated((self, _) =>
                    {
                        Log.Write($"{self.Name} Updated - " + i);
                        i++;

                        if (i >= 10)
                            self.Stop();
                    })
                    .OnStopped(self =>
                    {
                        Log.Write($"{self.Name} Stopped");
                        stopped = true;
                    })
                    .AssignTo(out var clip1)
                );


            while (!stopped)
            {
                executionCount++;
                if (executionCount >= 100)
                    Assert.Fail("Timeout");

                sequence.Update(0.1f);
            }
        }
    }
}
