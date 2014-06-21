namespace RfqStateMachine
{
    public enum RfqEvent
    {
        UserRequests,
        UserCancels,
        UserExecutes,

        ServerNewQuote,
        ServerQuoteError,
        ServerQuoteStreamComplete,
        ServerSendsExecutionReport,
        ServerExecutionError,
        ServerCancelled,
        ServerCancellationError,

        InternalError,
    }
}