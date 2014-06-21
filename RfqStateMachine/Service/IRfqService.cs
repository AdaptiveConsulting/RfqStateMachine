using System;
using System.Reactive;

namespace RfqStateMachine.Service
{
    public interface IRfqService
    {
        IObservable<IQuote> RequestQuoteStream(IQuoteRequest quoteRequest);
        IObservable<IExecutionReport> Execute(IExecutionRequest executionRequest);
        IObservable<Unit> Cancel(long rfqId);
    }
}