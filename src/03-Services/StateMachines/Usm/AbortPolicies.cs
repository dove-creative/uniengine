using System;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    [Flags]
    public enum AbortPolicies
    {
        None = 0,
        Self = 1,
        LowerPriority = 2,
        StopOnFailure = 4
    }
}

