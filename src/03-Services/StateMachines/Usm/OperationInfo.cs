using System;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public enum OperationStatus { NeverRun, Running, Success, Failure, Halted }

    public enum DetailedOperationStatus
    {
        NeverRun, Running, Success, Failure, HaltedBySelf, Preempted,
        ChildFailed, ChildCompleted, HaltedByParent, HaltedByExternal
    }

    public readonly struct OperationInfo
    {
        public DetailedOperationStatus DetailedOperationStatus { get; }
        public object Result { get; }
        public OperationStatus OperationStatus => DetailedOperationStatus switch
        {
            DetailedOperationStatus.NeverRun => OperationStatus.NeverRun,
            DetailedOperationStatus.Running => OperationStatus.Running,
            DetailedOperationStatus.Success or DetailedOperationStatus.ChildCompleted => OperationStatus.Success,
            DetailedOperationStatus.Failure or DetailedOperationStatus.ChildFailed => OperationStatus.Failure,
            _ => OperationStatus.Halted
        };
        public bool IsRunning => OperationStatus == OperationStatus.Running;
        public bool IsSuccess => OperationStatus == OperationStatus.Success;
        public bool IsFailure => OperationStatus == OperationStatus.Failure;
        public bool IsStopped => OperationStatus != OperationStatus.NeverRun && !IsRunning;


        public OperationInfo(DetailedOperationStatus status, object result = null)
        {
            if (status < DetailedOperationStatus.NeverRun || status > DetailedOperationStatus.HaltedByExternal)
                throw new ArgumentOutOfRangeException(nameof(status));

            DetailedOperationStatus = status;
            Result = result;
        }

        public bool TryGetResult<T>(out T result)
        {
            if (Result is T value)
            {
                result = value;
                return true;
            }

            result = default;
            return false;
        }
    }
}

