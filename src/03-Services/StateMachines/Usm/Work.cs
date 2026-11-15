using System;
using BlackThunder.UniEngine.StateMachines.Usm.Core;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public class Work : IWork
    {
        public object Owner { get; private set; }
        public object Blackboard { get; private set; }
        public event Action Started;
        public event Action Updated;
        public event Action<DetailedOperationStatus> Stopped;


        public Work OnStart(Action action) { Started += action; return this; }
        public Work OnUpdate(Action action) { Updated += action; return this; }
        public Work OnStopped(Action<DetailedOperationStatus> action) { Stopped += action; return this; }

        public virtual void Initialize(object owner, object blackboard)
        {
            Owner = owner;
            Blackboard = blackboard;
        }

        public virtual bool CheckCondition(object input) => true;
        public virtual void Start(object input) => Started?.Invoke();

        public virtual OperationInfo Update(object input)
        {
            Updated?.Invoke();
            return new OperationInfo(DetailedOperationStatus.Running, input);
        }

        public virtual void Stop(DetailedOperationStatus reason) => Stopped?.Invoke(reason);
    }
}

