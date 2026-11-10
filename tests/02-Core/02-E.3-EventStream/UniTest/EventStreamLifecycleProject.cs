using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using BlackThunder.UniTest;

namespace BlackThunder.UniEngine.Tests
{
    internal class EventStreamLifecycleProject<TCycle> :
        CompactProject<EventStreamLifecycleProject<TCycle>.LifecycleModel>, IDisposable
        where TCycle : Model, new()
    {
        // Internal
        public class LifecycleModel : Model
        {
            public TCycle Cycle { get; set; }
            public int Step { get; set; }
            public Func<int, IEnumerable<CompactLab<TCycle>>> CreateLabs { get; set; }
        }

        private readonly Action<TCycle> _disposeCycle;
        private readonly (string Prefix, Func<int, IEnumerable<CompactLab<TCycle>>> CreateLabs)[] _variants;
        private readonly ConcurrentDictionary<TCycle, byte> _activeCycles = new();


        // Content
        public EventStreamLifecycleProject(
            Action<TCycle> disposeCycle,
            params (string Prefix, Func<int, IEnumerable<CompactLab<TCycle>>> CreateLabs)[] variants)
            : base(ExecutionMode.Inline)
        {
            _disposeCycle = disposeCycle;
            _variants = variants;
        }

        public override IEnumerable<CompactLab<LifecycleModel>> CreateCompactLabs(LifecycleModel model)
        {
            if (model.Step != 0)
            {
                foreach (var lab in WrapLabs("", model.CreateLabs, model.Step)) yield return lab;
                yield break;
            }

            foreach (var (prefix, createLabs) in _variants)
            {
                foreach (var lab in WrapLabs(prefix, createLabs, 0)) yield return lab;
            }
        }

        private IEnumerable<CompactLab<LifecycleModel>> WrapLabs(
            string prefix,
            Func<int, IEnumerable<CompactLab<TCycle>>> createLabs,
            int step)
        {
            foreach (var lab in createLabs(step))
            {
                yield return new CompactLab<LifecycleModel>(
                    prefix + lab.Id,
                    arranger: model =>
                    {
                        if (step == 0)
                        {
                            model.Cycle = new TCycle();
                            model.CreateLabs = createLabs;
                            _activeCycles.TryAdd(model.Cycle, 0);
                        }

                        lab.Arranger?.Invoke(model.Cycle);
                    },
                    actor: model => lab.Actor?.Invoke(model.Cycle),
                    asserter: model =>
                    {
                        lab.Asserter?.Invoke(model.Cycle);
                        model.Step = step + 1;

                        // A verified terminal Lab closes this subject, so a new lifecycle can follow it.
                        if (lab.ToUncontinuable)
                        {
                            _disposeCycle(model.Cycle);
                            _activeCycles.TryRemove(model.Cycle, out _);
                            model.Cycle = null;
                            model.Step = 0;
                        }
                    });
            }
        }

        public void Dispose()
        {
            foreach (var cycle in _activeCycles.Keys) _disposeCycle(cycle);
            _activeCycles.Clear();
        }
    }
}
