
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reflection;
using NUnit.Framework;
using RfqStateMachine;
using Stateless;
using Tests.TestDoubles;

namespace Tests
{
    [TestFixture]
    public class RfqStateMachineTests
    {
        private RfqServiceDouble _rfqServiceDouble;
        private ConcurrencyServiceDouble _concurrencyServiceDouble;
        private IRfq _sut;
        private List<RfqUpdate> _updates;
        private Exception _error;
        private bool _completed;

        [SetUp]
        public void SetUp()
        {
            _rfqServiceDouble = new RfqServiceDouble();
            _concurrencyServiceDouble = new ConcurrencyServiceDouble();
            _sut = new Rfq(_rfqServiceDouble, _concurrencyServiceDouble);
            _updates = new List<RfqUpdate>();
            _error = null;
            _completed = false;

            _sut.Updates.Subscribe(
                _updates.Add,
                ex => _error = ex,
                () => _completed = true);
        }

        [Test]
        public void HappyPathScenario()
        {
            // user request quote
            _sut.RequestQuote(null);
            // server sends quote
            _rfqServiceDouble.RequestQuoteSubject.OnNext(null);
            // user executes
            _sut.Execute(null);
            // server sends execution report
            _rfqServiceDouble.ExecuteSubject.OnNext(null);

            Assert.AreEqual(5, _updates.Count);
            Assert.AreEqual(RfqState.Input, _updates[0].RfqState);
            Assert.AreEqual(RfqState.Requesting, _updates[1].RfqState);
            Assert.AreEqual(RfqState.Quoted, _updates[2].RfqState);
            Assert.AreEqual(RfqState.Executing, _updates[3].RfqState);
            Assert.AreEqual(RfqState.Done, _updates[4].RfqState);
        }

        [Test]
        public void MultipleServerQuotes()
        {
            // user request quote
            _sut.RequestQuote(null);
            // server sends quotes
            _rfqServiceDouble.RequestQuoteSubject.OnNext(null);
            _rfqServiceDouble.RequestQuoteSubject.OnNext(null);

            Assert.AreEqual(4, _updates.Count);
            Assert.AreEqual(RfqState.Input, _updates[0].RfqState);
            Assert.AreEqual(RfqState.Requesting, _updates[1].RfqState);
            Assert.AreEqual(RfqState.Quoted, _updates[2].RfqState);
            Assert.AreEqual(RfqState.Quoted, _updates[3].RfqState);
        }

        [Test]
        public void UserCancelRequest()
        {
            // user request quote
            _sut.RequestQuote(null);
            // user cancels
            _sut.Cancel(32);
            // server acks
            _rfqServiceDouble.CancelSubject.OnNext(Unit.Default);

            Assert.AreEqual(4, _updates.Count);
            Assert.AreEqual(RfqState.Input, _updates[0].RfqState);
            Assert.AreEqual(RfqState.Requesting, _updates[1].RfqState);
            Assert.AreEqual(RfqState.Cancelling, _updates[2].RfqState);
            Assert.AreEqual(RfqState.Cancelled, _updates[3].RfqState);
        }

        [Test]
        public void UserCancelQuote()
        {
            // user request quote
            _sut.RequestQuote(null);
            // server sends quotes
            _rfqServiceDouble.RequestQuoteSubject.OnNext(null);
            // user cancels
            _sut.Cancel(32);
            // server acks
            _rfqServiceDouble.CancelSubject.OnNext(Unit.Default);

            Assert.AreEqual(5, _updates.Count);
            Assert.AreEqual(RfqState.Input, _updates[0].RfqState);
            Assert.AreEqual(RfqState.Requesting, _updates[1].RfqState);
            Assert.AreEqual(RfqState.Quoted, _updates[2].RfqState);
            Assert.AreEqual(RfqState.Cancelling, _updates[3].RfqState);
            Assert.AreEqual(RfqState.Cancelled, _updates[4].RfqState);
        }

        [Test]
        public void GenerateStateDiagram()
        {
            var field = _sut.GetType().GetField("_stateMachine", BindingFlags.Instance | BindingFlags.NonPublic);
            var stateMachine = (StateMachine<RfqState, RfqEvent>) field.GetValue(_sut);
            Console.WriteLine(stateMachine.ToStateDiagram());
        }
    }
}
