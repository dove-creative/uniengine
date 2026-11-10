using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    internal static class RandomContinuousTest
    {
        public const int DefaultDepth = 100;


        // Content
        public static async Task RunAsync<TModel>(
            Project<TModel> project,
            int seed,
            int depth = DefaultDepth,
            CancellationToken cancellationToken = default,
            Action<string> reportWriter = null)
            where TModel : Model, new()
        {
            reportWriter ??= TestContext.Out.WriteLine;

            var stopwatch = Stopwatch.StartNew();
            var result = await project.ExecuteContinuously(depth, seed, cancellationToken);
            stopwatch.Stop();

            var report = result.GetReport();
            var reachedDepth = report.Nodes
                .Where(node => node.Status == "Success" || node.Status == "Failure")
                .Select(node => node.Depth)
                .DefaultIfEmpty()
                .Max();
            var succeeded = result.AllSucceed(out var cancelled);
            cancelled |= cancellationToken.IsCancellationRequested;

            var outcome = "Passed";
            if (!succeeded) outcome = "Failed";
            else if (cancelled) outcome = "Cancelled";
            else if (reachedDepth != depth) outcome = "Incomplete";

            var summary = FormattableString.Invariant(
                $"UniTest continuous seed={seed} depth={reachedDepth}/{depth} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3} outcome={outcome}");
            reportWriter(summary);

            // Parent IDs preserve the history of each candidate without repeating its prefix.
            foreach (var node in report.Nodes)
            {
                reportWriter($"{node.NodeId}\t{node.ParentId}\t{node.Depth}\t{node.Status}\t{node.LabId}");

                if (node.Error != null) reportWriter(node.Error.Details);
                if (node.ReportError != null) reportWriter(node.ReportError.Details);
            }

            Assert.That(succeeded, Is.True, summary);
            Assert.That(cancelled, Is.False, summary);
            Assert.That(reachedDepth, Is.EqualTo(depth), summary);
        }
    }
}
