using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.StateMachines.Usm;

namespace BlackThunder.UniEngine.Tests
{
    internal class UsmTestWork : Work
    {
        public string Name { get; }
        public List<string> Events { get; }
        public Func<object, bool> Condition { get; set; } = _ => true;
        public Func<object, object> Output { get; set; } = input => input;
        public Func<object, OperationInfo> Result { get; set; } = _ => new OperationInfo(DetailedOperationStatus.Running);
        public Action Starting { get; set; }
        public Action Updating { get; set; }
        public Action Stopping { get; set; }
        public Action Initializing { get; set; }
        public int Starts { get; private set; }
        public int Updates { get; private set; }
        public int Stops { get; private set; }
        public DetailedOperationStatus StopReason { get; private set; }

        public UsmTestWork(string name = "work", List<string> events = null)
        {
            Name = name;
            Events = events ?? new List<string>();
        }

        public override void Initialize(object owner, object blackboard)
        {
            base.Initialize(owner, blackboard);
            Initializing?.Invoke();
        }

        public override bool CheckCondition(object input) => Condition(input);

        public override void Start(object input)
        {
            Starts++;
            Events.Add($"{Name}:start");
            Starting?.Invoke();
        }

        public override OperationInfo Update(object input)
        {
            Updates++;
            Events.Add($"{Name}:update:{input}");
            Updating?.Invoke();
            var output = Output(input);
            return new OperationInfo(Result(input).DetailedOperationStatus, output);
        }

        public override void Stop(DetailedOperationStatus reason)
        {
            Stops++;
            StopReason = reason;
            Events.Add($"{Name}:stop:{reason}");
            Stopping?.Invoke();
        }
    }
}
