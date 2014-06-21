using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using RfqStateMachine.Service;
using RfqStateMachine.Utils;
using Stateless;

namespace RfqStateMachine
{
    public class Rfq : IRfq
    {
        private readonly IRfqService _rfqService;
        private readonly IConcurrencyService _concurrencyService;
        private readonly StateMachine<RfqState, RfqEvent> _stateMachine;
        private readonly ISubject<RfqUpdate> _rfqUpdateSubject;
        private readonly SerialDisposable _requestSubscription = new SerialDisposable();
        private readonly SerialDisposable _cancellationSubscription = new SerialDisposable();
        private readonly SerialDisposable _executionSubscription = new SerialDisposable();
        private readonly CompositeDisposable _disposables;

        // strongly typed triggers (events)
        private StateMachine<RfqState, RfqEvent>.TriggerWithParameters<IQuote> _rfqEventServerSendsQuote;
        private StateMachine<RfqState, RfqEvent>.TriggerWithParameters<IExecutionReport> _rfqEventServerSendsExecutionReport;
        private StateMachine<RfqState, RfqEvent>.TriggerWithParameters<IQuoteRequest> _rfqEventUserRequests;
        private StateMachine<RfqState, RfqEvent>.TriggerWithParameters<IExecutionRequest> _rfqEventUserExecutes;
        private StateMachine<RfqState, RfqEvent>.TriggerWithParameters<long> _rfqEventUserCancels;
        private StateMachine<RfqState, RfqEvent>.TriggerWithParameters<Exception> _rfqEventServerQuoteError;
        private StateMachine<RfqState, RfqEvent>.TriggerWithParameters<Exception> _rfqEventServerCancellationError;
        private StateMachine<RfqState, RfqEvent>.TriggerWithParameters<Exception> _rfqEventServerExecutionError;

        public Rfq(IRfqService rfqService, IConcurrencyService concurrencyService)
        {
            _rfqService = rfqService;
            _concurrencyService = concurrencyService;
            _stateMachine = new StateMachine<RfqState, RfqEvent>(RfqState.Input);
            _rfqUpdateSubject = new BehaviorSubject<RfqUpdate>(new RfqUpdate(RfqState.Input, null, null));
            _disposables = new CompositeDisposable(_cancellationSubscription, _executionSubscription, _requestSubscription);

            CreateStateMachine();
        }

        /* ----------------------------------------
         *              PUBLIC API
         *              
         *  /!\ Only statemachine transitions allowed here /!\ 
         * 
         * ---------------------------------------*/

        public void RequestQuote(IQuoteRequest quoteRequest)
        {
            _stateMachine.Fire(_rfqEventUserRequests, quoteRequest);
        }

        public void Cancel(long rfqId)
        {
            _stateMachine.Fire(_rfqEventUserCancels, rfqId);
        }

        public void Execute(IExecutionRequest executionRequest)
        {
            _stateMachine.Fire(_rfqEventUserExecutes, executionRequest);
        }

        public IObservable<RfqUpdate> Updates { get { return _rfqUpdateSubject; } }

        /* ----------------------------------------
        *           STATEMACHINE DEFINITION
        * ---------------------------------------*/

        private void CreateStateMachine()
        {
            // define strongly typed triggers (events)
            _rfqEventServerSendsQuote = _stateMachine.SetTriggerParameters<IQuote>(RfqEvent.ServerNewQuote);
            _rfqEventServerSendsExecutionReport = _stateMachine.SetTriggerParameters<IExecutionReport>(RfqEvent.ServerSendsExecutionReport);
            _rfqEventServerQuoteError = _stateMachine.SetTriggerParameters<Exception>(RfqEvent.ServerQuoteError);
            _rfqEventServerCancellationError = _stateMachine.SetTriggerParameters<Exception>(RfqEvent.ServerCancellationError);
            _rfqEventServerExecutionError = _stateMachine.SetTriggerParameters<Exception>(RfqEvent.ServerExecutionError);
            _rfqEventUserRequests = _stateMachine.SetTriggerParameters<IQuoteRequest>(RfqEvent.UserRequests);
            _rfqEventUserExecutes = _stateMachine.SetTriggerParameters<IExecutionRequest>(RfqEvent.UserExecutes);
            _rfqEventUserCancels = _stateMachine.SetTriggerParameters<long>(RfqEvent.UserCancels);

            _stateMachine.OnUnhandledTrigger(OnUnhandledTrigger);

            _stateMachine.Configure(RfqState.Input)
                .OnEntry(LogTransition)
                .Permit(RfqEvent.UserRequests, RfqState.Requesting);

            _stateMachine.Configure(RfqState.Requesting)
                .OnEntry(LogTransition)
                .OnEntryFrom(_rfqEventUserRequests, OnEntryRequesting)
                .Permit(RfqEvent.ServerNewQuote, RfqState.Quoted)
                .Permit(RfqEvent.UserCancels, RfqState.Cancelling)
                .Permit(RfqEvent.InternalError, RfqState.Error);

            _stateMachine.Configure(RfqState.Quoted)
                .OnEntry(LogTransition)
                .OnEntryFrom(_rfqEventServerSendsQuote, OnEntryQuoted)
                .PermitReentry(RfqEvent.ServerNewQuote)
                .Permit(RfqEvent.UserCancels, RfqState.Cancelling)
                .Permit(RfqEvent.UserExecutes, RfqState.Executing)
                .OnExit(OnExitQuoted);

            _stateMachine.Configure(RfqState.Executing)
                .OnEntry(LogTransition)
                .OnEntryFrom(_rfqEventUserExecutes, OnEntryExecuting)
                .Permit(RfqEvent.ServerSendsExecutionReport, RfqState.Done);

            _stateMachine.Configure(RfqState.Cancelling)
                .Permit(RfqEvent.ServerCancelled, RfqState.Cancelled)
                .OnEntry(LogTransition)
                .OnEntryFrom(_rfqEventUserCancels, OnEntryCancelling);

            _stateMachine.Configure(RfqState.Cancelled)
                .OnEntry(LogTransition)
                .OnEntry(OnEntryCancelled);

            _stateMachine.Configure(RfqState.Done)
                .OnEntry(LogTransition)
                .OnEntryFrom(_rfqEventServerSendsExecutionReport, OnEntryDone);

            _stateMachine.Configure(RfqState.Error)
                .OnEntry(LogTransition)
                .OnEntryFrom(_rfqEventServerQuoteError, OnEntryError)
                .OnEntryFrom(_rfqEventServerExecutionError, OnEntryError);
        }

        /* ----------------------------------------
         *     STATEMACHINE TRANSITION HANDLERS
         *
         *  All internal logic should be handled here. 
         *  For server calls, server callbacks (ie. responses, errors, etc) 
         *  are only allowed to transition the state machine
         * ---------------------------------------*/

        private void OnEntryQuoted(IQuote quote)
        {  
            _rfqUpdateSubject.OnNext(new RfqUpdate(RfqState.Quoted, quote, null));
        }

        private void OnEntryRequesting(IQuoteRequest quoteRequest)
        {
            _rfqUpdateSubject.OnNext(new RfqUpdate(RfqState.Requesting, null, null));

            _requestSubscription.Disposable = _rfqService.RequestQuoteStream(quoteRequest)
                .Timeout(TimeSpan.FromSeconds(5))
                .ObserveOn(_concurrencyService.Dispatcher)
                .SubscribeOn(_concurrencyService.TaskPool)
                .Subscribe(
                    quote => _stateMachine.Fire(_rfqEventServerSendsQuote, quote),
                    ex => _stateMachine.Fire(_rfqEventServerQuoteError, ex),
                    () => _stateMachine.Fire(RfqEvent.ServerQuoteStreamComplete));
        }

        private void OnEntryCancelling(long rfqId, StateMachine<RfqState, RfqEvent>.Transition transition)
        {
            _rfqUpdateSubject.OnNext(new RfqUpdate(RfqState.Cancelling, null, null));

            _cancellationSubscription.Disposable = _rfqService.Cancel(rfqId)
                 .Timeout(TimeSpan.FromSeconds(5))
                .ObserveOn(_concurrencyService.Dispatcher)
                .SubscribeOn(_concurrencyService.TaskPool)
                .Subscribe(
                    _ => _stateMachine.Fire(RfqEvent.ServerCancelled),
                    ex => _stateMachine.Fire(_rfqEventServerCancellationError, ex));
        }
        
        private void OnEntryExecuting(IExecutionRequest executionRequest)
        {
            _rfqUpdateSubject.OnNext(new RfqUpdate(RfqState.Executing, null, null));

            _executionSubscription.Disposable = _rfqService.Execute(executionRequest)
                .Timeout(TimeSpan.FromSeconds(5))
                .ObserveOn(_concurrencyService.Dispatcher)
                .SubscribeOn(_concurrencyService.TaskPool)
                .Subscribe(
                    executionReport => _stateMachine.Fire(_rfqEventServerSendsExecutionReport, executionReport),
                    ex => _stateMachine.Fire(_rfqEventServerExecutionError, ex));
        }

        private void OnExitQuoted()
        {
            // we no longer need the quote stream, unsubscribe
            _requestSubscription.Dispose();
        }

        private void OnEntryDone(IExecutionReport executionReport)
        {
            _rfqUpdateSubject.OnNext(new RfqUpdate(RfqState.Done, null, executionReport));
        }

        private void OnEntryCancelled(StateMachine<RfqState, RfqEvent>.Transition transition)
        {
            _rfqUpdateSubject.OnNext(new RfqUpdate(RfqState.Cancelled, null, null));
            _rfqUpdateSubject.OnCompleted();
        }

        private void OnEntryError(Exception ex)
        {
            _rfqUpdateSubject.OnError(ex);
        }

        /* ----------------------------------------
        *           INTERNAL STUFF
        * ---------------------------------------*/

        private void LogTransition(StateMachine<RfqState, RfqEvent>.Transition transition)
        {
            Console.WriteLine("[Event {0}] {1} --> {2}", transition.Trigger, transition.Source, transition.Destination);
        }

        private void OnUnhandledTrigger(RfqState state, RfqEvent trigger)
        {
            var message = string.Format("State machine received an invalid trigger '{0}' in state '{1}'", trigger, state);
            Console.WriteLine(message);

            _rfqUpdateSubject.OnError(new ApplicationException(message));
            // show some technical error to the user or transition to error state
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
