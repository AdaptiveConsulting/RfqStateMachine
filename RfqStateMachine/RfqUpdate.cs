using RfqStateMachine.Service;

namespace RfqStateMachine
{
    public class RfqUpdate
    {
        public RfqState RfqState { get; private set; }
        public IQuote Quote { get; private set; }
        public IExecutionReport ExecutionReport { get; private set; }

        public RfqUpdate(RfqState rfqState, IQuote quote, IExecutionReport executionReport)
        {
            RfqState = rfqState;
            Quote = quote;
            ExecutionReport = executionReport;
        }
    }
}