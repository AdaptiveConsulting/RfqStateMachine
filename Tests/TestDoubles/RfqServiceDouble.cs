using System;
using System.Reactive;
using System.Reactive.Subjects;
using RfqStateMachine.Service;

namespace Tests.TestDoubles
{
    public class RfqServiceDouble : IRfqService
    {
        public RfqServiceDouble()
        {
            RequestQuoteSubject = new Subject<IQuote>();
            ExecuteSubject = new Subject<IExecutionReport>();
            CancelSubject = new Subject<Unit>();
        }

        public ISubject<IQuote> RequestQuoteSubject { get; private set; }
        public ISubject<IExecutionReport> ExecuteSubject { get; private set; }
        public ISubject<Unit> CancelSubject { get; private set; } 

        public IObservable<IQuote> RequestQuoteStream(IQuoteRequest quoteRequest)
        {
            return RequestQuoteSubject;
        }

        public IObservable<IExecutionReport> Execute(IExecutionRequest executionRequest)
        {
            return ExecuteSubject;
        }

        public IObservable<Unit> Cancel(long rfqId)
        {
            return CancelSubject;
        }
    }
}