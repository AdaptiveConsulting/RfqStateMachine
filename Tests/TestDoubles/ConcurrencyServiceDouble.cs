using System.Reactive.Concurrency;
using RfqStateMachine.Utils;

namespace Tests.TestDoubles
{
    public class ConcurrencyServiceDouble : IConcurrencyService
    {
        public ConcurrencyServiceDouble()
        {
            Dispatcher = ImmediateScheduler.Instance;
            TaskPool = ImmediateScheduler.Instance;
        }

        public IScheduler Dispatcher { get; private set; }
        public IScheduler TaskPool { get; private set; }
    }
}
