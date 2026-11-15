using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.StateMachines.Usm.Core;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public enum ParallelCompletionPolicy { AllSuccess, AnySuccess }

    public class Parallel : Transitioner
    {
        public ParallelCompletionPolicy CompletionPolicy { get; }
        private OperationInfo[] _results;


        public Parallel(ParallelCompletionPolicy completionPolicy = ParallelCompletionPolicy.AllSuccess)
        {
            if (completionPolicy != ParallelCompletionPolicy.AllSuccess
                && completionPolicy != ParallelCompletionPolicy.AnySuccess)
                throw new ArgumentOutOfRangeException(nameof(completionPolicy));

            CompletionPolicy = completionPolicy;
        }

        public override void Start(object input, IFrameContext context)
        {
            base.Start(input, context);
            _results = new OperationInfo[Children.Count];
        }

        public override OperationInfo Update(IFrameContext context)
        {
            var frame = FrameContext.Require(context);
            if (!IsRunning) return Running(frame);
            var input = frame.Payload;
            var allSuccess = CompletionPolicy == ParallelCompletionPolicy.AllSuccess;

            for (var i = 0; i < Children.Count; i++)
            {
                if (_results[i].IsStopped) continue;
                var child = Children[i];
                frame.Payload = input;
                var check = _results[i].OperationStatus == OperationStatus.NeverRun
                    || (child.AbortPolicies & AbortPolicies.Self) != 0;
                _results[i] = UpdateChild(child, frame, check);
                if (!IsRunning) return Running(frame);

                if (_results[i].IsStopped && (_results[i].IsSuccess != allSuccess))
                    return Finish(!allSuccess, frame);
            }

            foreach (var result in _results)
            {
                if (result.IsRunning) return Snapshot(DetailedOperationStatus.Running);
            }

            return Finish(allSuccess, frame);
        }

        private OperationInfo Finish(bool success, FrameContext frame)
        {
            List<Exception> errors = null;
            for (var i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (!child.IsActive) continue;
                try
                {
                    child.Stop(
                        frame.GetOrAddNext(child.Id, child.LastResult.Result),
                        DetailedOperationStatus.HaltedByParent);
                }
                catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
                finally { _results[i] = child.LastResult; }
            }

            if (errors?.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors != null) throw new AggregateException(errors);
            return Snapshot(success ? DetailedOperationStatus.ChildCompleted : DetailedOperationStatus.ChildFailed);
        }

        private OperationInfo Snapshot(DetailedOperationStatus status) =>
            new OperationInfo(status, Array.AsReadOnly((OperationInfo[])_results.Clone()));
    }
}

