namespace RfqStateMachine
{
    public enum RfqState
    {
        Input,
        Requesting,
        Cancelling,
        Cancelled,
        Quoted,
        Executing,
        Error,
        Done
    }
}