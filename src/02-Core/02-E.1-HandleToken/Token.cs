using System;

namespace Blackthunder.UniEngine.Core
{
    public class Token : Handle
    {
        // Front
        public event Action Action
        {
            add
            {
                ThrowIfDisposed();
                Disposed += value;
            }
            remove
            {
                if (IsDisposed) return;
                Disposed -= value;
            }
        }

        // Content
        public Token() { }
        public Token(Action action)
            : base(action) { }
    }
}
