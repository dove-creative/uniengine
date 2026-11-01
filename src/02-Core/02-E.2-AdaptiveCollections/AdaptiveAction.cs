using System;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public class AdaptiveAction : AdaptiveCollection<Action>
    {
        public AdaptiveAction()
            : base() { }
        public AdaptiveAction(bool deferredUpdate)
            : base(deferredUpdate) { }

        public void Invoke()
        {
            foreach (var action in this)
            {
                if (action == null) continue;

                foreach (Action callback in action.GetInvocationList())
                {
                    if (!IsCurrentItemActive) break;

                    try
                    {
                        callback();
                    }
                    catch (Exception exception)
                    {
                        RemoveCurrentItem();
                        ExceptionHandler.Report(exception);
                        break;
                    }
                }
            }
        }
    }

    public class AdaptiveAction<T> : AdaptiveCollection<Action<T>>
    {
        public AdaptiveAction()
            : base() { }
        public AdaptiveAction(bool deferredUpdate)
            : base(deferredUpdate) { }

        public void Invoke(T t)
        {
            foreach (var action in this)
            {
                if (action == null) continue;

                foreach (Action<T> callback in action.GetInvocationList())
                {
                    if (!IsCurrentItemActive) break;

                    try
                    {
                        callback(t);
                    }
                    catch (Exception exception)
                    {
                        RemoveCurrentItem();
                        ExceptionHandler.Report(exception);
                        break;
                    }
                }
            }
        }
    }

    public class AdaptiveAction<T, U> : AdaptiveCollection<Action<T, U>>
    {
        public AdaptiveAction()
            : base() { }
        public AdaptiveAction(bool deferredUpdate)
            : base(deferredUpdate) { }

        public void Invoke(T t, U u)
        {
            foreach (var action in this)
            {
                if (action == null) continue;

                foreach (Action<T, U> callback in action.GetInvocationList())
                {
                    if (!IsCurrentItemActive) break;

                    try
                    {
                        callback(t, u);
                    }
                    catch (Exception exception)
                    {
                        RemoveCurrentItem();
                        ExceptionHandler.Report(exception);
                        break;
                    }
                }
            }
        }
    }

    public class AdaptiveAction<T, U, V> : AdaptiveCollection<Action<T, U, V>>
    {
        public AdaptiveAction()
            : base() { }
        public AdaptiveAction(bool deferredUpdate)
            : base(deferredUpdate) { }

        public void Invoke(T t, U u, V v)
        {
            foreach (var action in this)
            {
                if (action == null) continue;

                foreach (Action<T, U, V> callback in action.GetInvocationList())
                {
                    if (!IsCurrentItemActive) break;

                    try
                    {
                        callback(t, u, v);
                    }
                    catch (Exception exception)
                    {
                        RemoveCurrentItem();
                        ExceptionHandler.Report(exception);
                        break;
                    }
                }
            }
        }
    }
}
