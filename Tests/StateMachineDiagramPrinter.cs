using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stateless;

namespace Tests
{
    public class StateMachineDiagramPrinter<TState, TTrigger>
    {
        private const string FullTemplate = @"digraph g{{
{0}

{1}
}}";

        private readonly StateMachine<TState, TTrigger> _stateMachine;

        public StateMachineDiagramPrinter(StateMachine<TState, TTrigger> stateMachine)
        {
            _stateMachine = stateMachine;
        }

        public string ToDiagram()
        {
            var stateConfigField = _stateMachine.GetType().GetField("_stateConfiguration", BindingFlags.Instance | BindingFlags.NonPublic);

            var stateConfig = stateConfigField.GetValue(_stateMachine) as IEnumerable;

            var results = IterateStates(stateConfig).ToList();

            var allStates = results.SelectMany(t => new[] {t.From, t.To}).Distinct().Select(t => t.ToString());

            return string.Format(FullTemplate, ToString(allStates), ToString(results));

        }

        private IEnumerable<Transition> IterateStates(IEnumerable stateConfig)
        {
            foreach (object o in stateConfig)
            {
                var fromState = (TState) GetPublicProperty(o, "Key");

                var value = GetPublicProperty(o, "Value");

                var triggersField = (IEnumerable) GetPrivateField(value, "_triggerBehaviours");

                foreach (var trigger in triggersField)
                {
                    var triggerValue = (TTrigger) GetPublicProperty(trigger, "Key");
                    var triggerValues = (IEnumerable) GetPublicProperty(trigger, "Value");

                    foreach (var transitionValue in triggerValues)
                    {
                        var toState = (TState) GetPrivateField(transitionValue, "_destination");

                        yield return new Transition(fromState, triggerValue, toState);
                    }
                }
            }
        }

        private object GetPublicProperty(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property.GetValue(source);
        }

        private object GetPrivateField(object source, string fieldName)
        {
            var field = source.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field.GetValue(source);
        }

        private static string ToString(IEnumerable<string> states)
        {
            return string.Join(";\n", states) + ";\nnode[shape=plaintext]";
        }

        private static string ToString(IEnumerable<Transition> transitions)
        {
            return string.Join("\n", transitions.Select(t => string.Format("{0} -> {2} [label={1}]", t.From, t.Trigger, t.To)));
        }

        private class Transition
        {
            public TState From { get; private set; }
            public TTrigger Trigger { get; private set; }
            public TState To { get; private set; }

            public Transition(TState @from, TTrigger transition, TState to)
            {
                From = @from;
                Trigger = transition;
                To = to;
            }
        }
    }
}