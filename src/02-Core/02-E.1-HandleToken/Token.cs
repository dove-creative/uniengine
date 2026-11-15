using System;

namespace BlackThunder.UniEngine.Core
{
    public class Token : Handle
    {
        // Control
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
        public Token(Action action) : base(action) { }
    }
}
