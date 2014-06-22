# RFQ State Machine

We highlighted quite a few techniques to deal with reactive systems in [Reactive Trader](https://github.com/AdaptiveConsulting/ReactiveTrader). There is another one, that we commonly use, that was not demonstrated. 
A state machine is a simple yet powerfull way of decomposing some functionality into states and a set of valid transition between them. 
When you find yourself dealing for instance with user input and/or server events and see lots of branching in your code (if/switch statements) on some _state variables, chances are high that a statemachine could be introduced to simplify things.

In this post we will look at a concreate usecase, we will define a state machine for it and we will see how we can organise our code around the state machnine and interact with it. 

## Example: RFQ workflow

In finance Request For Quote (RFQ) is a common mechanism used to request a price electronically: the client submits a request to the pricing server. 
At some point the server provides a quote (or a serie of quotes) and the client can decide to execute (HIT the price) or pass (cancel).
We are going to build a state machine that would live client side, in some UI application like reactive trader, to control the state of a RFQ.

The following diagram describes the different states of the RFQ and the possible transitions.

![state machine diagram](https://github.com/AdaptiveConsulting/RfqStateMachine/blob/master/StateMachine.PNG)

This is a visual representation of a statemachine, it contains 

 - states:
   - an initial state (at the top),
   - intermediate states (requesting, quoted, Executing)
   - terminal states (Error, Done, Canceled)
 - and transitions between states (arrows)

I find those diagrams very helpfull to think about the system, while building them I will generally go through all the states I already discovered and ask myself the following questions: 

 - could anything else happen in this state? 
 - Any unhappy path? (ie. timeout, error, etc) 
 - Do we have a representation of this particular state for the corresponding UI? (if you are building a UI).

Those diagrams are also very usefull to discuss with non developers: business people, UX, etc.

## From diagram to code

Statemachines can be implemented in many differents ways, either from scratch or using some library. 
For any decent size statemachine I tend to use [Stateless](https://code.google.com/p/stateless/) but the recommendations that follow would stand for any library or hand written statemachine.

I like to define state machines in a single place: I find that spreading the definition accross multiple files/classes makes it harder to understand (yes, I'm not a fan of the State design pattern..)

Stateless offers a nice fluent syntax to define states and possible transitions. 

###Defining states


The first thing we need to do is to define the set of states, we can use an enum for that:

```csharp
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
```
[gist](https://gist.github.com/odeheurles/9160c1b3e0687d3ebf38#file-rfqstates)

###Events

Then we define the events which will trigger transitions between states. In our case those are events coming from the client or from the server. 

Again we can use an enum to define those events.


```csharp
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
```
[gist](https://gist.github.com/odeheurles/14bbd2a0b8040a808356#file-rfqevent)

I like to prefix those events with their origin, just to makes things explicit (here we have 'Server', 'User', 'Internal')

As you can see events coming from the server always expose a happy path (for instance ServerNewQuote when the server sends a new quote) and at least one corresponding error event (ServerQuoteError).

![state machine diagram](https://github.com/AdaptiveConsulting/RfqStateMachine/blob/master/Components.PNG)


You will also often have internal events, for instance a timer expiring can raise an internal event to trigger a state transition.

Events may or not carry some data: for instance UserRequests event needs to contain the description of the product being priced.
For those events requiring parameters it is useful to define strongly typed events. 

This is how we declare them with Stateless, for instance for the ServerSendsQuote event:

```csharp
_rfqEventServerSendsQuote = _stateMachine.SetTriggerParameters<IQuote>(RfqEvent.ServerNewQuote);
```
[gist](https://gist.github.com/odeheurles/dea91fa626e6b468ef07#file-stronglytypedevent)

Defining transitions
--------------------

Now we can define transitions. For each state we define which events are allowed and when they are triggered to which state we will transition.
This is very straight forward with stateless:

```csharp
_stateMachine.Configure(RfqState.Input)
             .Permit(RfqEvent.UserRequests, RfqState.Requesting);

_stateMachine.Configure(RfqState.Requesting)
             .Permit(RfqEvent.ServerNewQuote, RfqState.Quoted)
             .Permit(RfqEvent.UserCancels, RfqState.Cancelling)
             .Permit(RfqEvent.InternalError, RfqState.Error);

_stateMachine.Configure(RfqState.Quoted)
             .PermitReentry(RfqEvent.ServerNewQuote)
             .Permit(RfqEvent.UserCancels, RfqState.Cancelling)
             .Permit(RfqEvent.UserExecutes, RfqState.Executing);

_stateMachine.Configure(RfqState.Executing)
             .Permit(RfqEvent.ServerSendsExecutionReport, RfqState.Done);

_stateMachine.Configure(RfqState.Cancelling)
             .Permit(RfqEvent.ServerCancelled, RfqState.Cancelled);
```
[gist](https://gist.github.com/odeheurles/2a0ef6112f33d9f2425d)

###Defining behavior

When we send an event to the state machine, two things can happen, the current state has a valid transition for this event or not. 

If the current state can accept an event we generally want to execute our code at some point around the transition:

 - when you enter a state (or re-enter a state since it's also possible to have transitions looping back on the same state)
 - when you exit a state
 - upon transition, if you have different behavior to implement for different transitions leading to a same state

I tend to apply actions upon entry into a state and use the other variants 

**Important: when implementing a statemachine, you want to put all your logic inside those actions (on state entry, on state exit, on transition) because the state machine has already checked that the incoming event was valid for the current state.**

Here is an example with stateless syntax. When the use request we want to log the transition and also to prtform some logic on entry in the requesting state:

```csharp
_stateMachine.Configure(RfqState.Requesting)
                .OnEntry(LogTransition)
                .OnEntryFrom(_rfqEventUserRequests, OnEntryRequesting)
                .Permit(RfqEvent.ServerNewQuote, RfqState.Quoted)
                .Permit(RfqEvent.UserCancels, RfqState.Cancelling)
                .Permit(RfqEvent.InternalError, RfqState.Error);

private void OnEntryRequesting(IQuoteRequest quoteRequest)
{
    _requestSubscription.Disposable = _rfqService.RequestQuoteStream(quoteRequest)
        .Timeout(TimeSpan.FromSeconds(5))
        .ObserveOn(_concurrencyService.Dispatcher)
        .SubscribeOn(_concurrencyService.TaskPool)
        .Subscribe(
            quote => _stateMachine.Fire(_rfqEventServerSendsQuote, quote),
            ex => _stateMachine.Fire(_rfqEventServerQuoteError, ex),
            () => _stateMachine.Fire(RfqEvent.ServerQuoteStreamComplete));
}
```