using Stateless;

namespace Tests
{
    public static class StateMachineExtensions
    {
        public static string ToStateDiagram<TState, TTrigger>(this StateMachine<TState, TTrigger> stateMachine)
        {
            var printer = new StateMachineDiagramPrinter<TState, TTrigger>(stateMachine);
            return printer.ToDiagram();
        }
    }
}