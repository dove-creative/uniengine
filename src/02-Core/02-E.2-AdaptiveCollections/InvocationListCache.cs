using System;
using System.Runtime.CompilerServices;

namespace Blackthunder.UniEngine.Core
{
    internal static class InvocationListCache<TDelegate> where TDelegate : Delegate
    {
        // Internal
        private static readonly ConditionalWeakTable<TDelegate, Delegate[]> _entries = new();
        private static readonly ConditionalWeakTable<TDelegate, Delegate[]>.CreateValueCallback _createInvocationList = CreateInvocationList;


        // Content
        // Weak keys allow removed delegates and their targets to be collected together.
        public static Delegate[] Get(TDelegate action) => _entries.GetValue(action, _createInvocationList);

        private static Delegate[] CreateInvocationList(TDelegate action) => action.GetInvocationList();
    }
}
