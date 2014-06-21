using System;
using RfqStateMachine.Service;

namespace RfqStateMachine
{
    public interface IRfq : IDisposable
    {
        void RequestQuote(IQuoteRequest quoteRequest);
        void Cancel(long rfqId);
        void Execute(IExecutionRequest quote);

        IObservable<RfqUpdate> Updates { get; } 
    }
}