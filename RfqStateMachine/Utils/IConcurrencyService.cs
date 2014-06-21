using System.Reactive.Concurrency;

namespace RfqStateMachine.Utils
{
    public interface IConcurrencyService
    {
        IScheduler Dispatcher { get; }
        IScheduler TaskPool { get; }
    }
}
