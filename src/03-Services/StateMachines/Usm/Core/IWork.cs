namespace BlackThunder.UniEngine.StateMachines.Usm.Core
{
    public interface IWork
    {
        void Initialize(object owner, object blackboard);
        bool CheckCondition(object input);
        void Start(object input);
        OperationInfo Update(object input);
        void Stop(DetailedOperationStatus reason);
    }
}

