module Wanxiangshu.Shell.ChildSessionMailbox

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.PromiseQueue

type TurnOutcome =
    | TaskCompleted of output: string
    | Failed of ErrorInput
    | EndedWithoutTaskComplete
    | Cancelled

type Deferred<'T> =
    { Resolve: 'T -> unit
      Reject: exn -> unit
      Promise: JS.Promise<'T> }

let createDeferred<'T> () : Deferred<'T> =
    let mutable resolveFn = fun _ -> ()
    let mutable rejectFn = fun _ -> ()

    let p =
        Promise.create (fun resolve reject ->
            resolveFn <- resolve
            rejectFn <- reject)

    { Resolve = resolveFn
      Reject = rejectFn
      Promise = p }

type ISessionTurnHost =
    abstract RunOneTurn: sessionId: string * model: FallbackModel * prompt: string -> JS.Promise<TurnOutcome>

type TurnState =
    | Ready
    | AwaitingStart of turnId: string * reply: Deferred<TurnOutcome>
    | Active of turnId: string * reply: Deferred<TurnOutcome> * pendingOutcome: TurnOutcome option
    | Aborting of turnId: string * reply: Deferred<TurnOutcome> * finalOutcome: TurnOutcome

type Command =
    | RunTurn of
        model: FallbackModel *
        prompt: string *
        turnId: string *
        sendFn: (string -> JS.Promise<unit>) *
        reply: Deferred<TurnOutcome>
    | TurnStarted of turnId: string
    | TurnError of ErrorInput
    | TaskComplete of output: string
    | SessionIdle
    | Timeout of turnId: string
    | AbortCompleted

type ChildSessionMailbox(onHostAbort: unit -> unit) as this =
    let queue = SerialQueue()
    let mutable state = Ready
    let mutable poison = false
    let mutable watchdogTimerId = None
    let mutable hardTimeoutTimerId = None

    let clearTimers () =
        watchdogTimerId |> Option.iter (fun id -> JS.clearTimeout id)
        watchdogTimerId <- None
        hardTimeoutTimerId |> Option.iter (fun id -> JS.clearTimeout id)
        hardTimeoutTimerId <- None

    let rec transition (state: TurnState) (cmd: Command) : TurnState =
        match state, cmd with
        | Ready, RunTurn(model, prompt, turnId, sendFn, reply) ->
            if poison then
                reply.Reject(failwith "InfrastructureFailure: session is poisoned due to hard timeout")
                Ready
            else
                clearTimers ()

                // Start watchdog timer (5 minutes)
                let timer = JS.setTimeout (fun () -> this.Post(Timeout turnId) |> ignore) 300000
                watchdogTimerId <- Some timer

                sendFn turnId |> ignore
                AwaitingStart(turnId, reply)

        | AwaitingStart(expectedId, reply), TurnStarted(turnId) when turnId = expectedId -> Active(turnId, reply, None)

        | AwaitingStart(_, _), TurnStarted(_) -> state

        | AwaitingStart(_, _), SessionIdle -> state

        | AwaitingStart(expectedId, reply), TurnError(error) -> Active(expectedId, reply, Some(Failed error))

        | AwaitingStart(expectedId, reply), TaskComplete(output) ->
            Active(expectedId, reply, Some(TaskCompleted output))

        | AwaitingStart(expectedId, reply), Timeout(turnId) when turnId = expectedId ->
            clearTimers ()
            onHostAbort ()
            // Start hard timeout timer (1 minute)
            let timer = JS.setTimeout (fun () -> this.Post(AbortCompleted) |> ignore) 60000
            hardTimeoutTimerId <- Some timer
            Aborting(expectedId, reply, Cancelled)

        | Active(expectedId, reply, None), TurnError(error) -> Active(expectedId, reply, Some(Failed error))

        | Active(expectedId, reply, None), TaskComplete(output) -> Active(expectedId, reply, Some(TaskCompleted output))

        | Active(_, reply, Some outcome), SessionIdle ->
            clearTimers ()
            reply.Resolve outcome
            Ready

        | Active(_, reply, None), SessionIdle ->
            clearTimers ()
            reply.Resolve EndedWithoutTaskComplete
            Ready

        | Active(expectedId, reply, _), Timeout(turnId) when turnId = expectedId ->
            clearTimers ()
            onHostAbort ()
            // Start hard timeout timer (1 minute)
            let timer = JS.setTimeout (fun () -> this.Post(AbortCompleted) |> ignore) 60000
            hardTimeoutTimerId <- Some timer
            Aborting(expectedId, reply, Cancelled)

        | Aborting(_, reply, finalOutcome), SessionIdle ->
            clearTimers ()
            reply.Resolve finalOutcome
            Ready

        | Aborting(_, reply, _), AbortCompleted ->
            clearTimers ()
            poison <- true // Poison the session
            reply.Reject(failwith "InfrastructureFailure: session is poisoned due to hard timeout after abort")
            Ready

        | _ -> state

    member _.IsPoisoned = poison
    member _.MarkPoisoned() = poison <- true
    member _.GetState() = state

    member _.Post(cmd: Command) : JS.Promise<unit> =
        queue.Enqueue(fun () -> promise { state <- transition state cmd })

module ChildSessionMailboxRegistry =
    let mutable private mailboxes = Map.empty<string, ChildSessionMailbox>

    let TryGet (sessionId: string) : ChildSessionMailbox option = Map.tryFind sessionId mailboxes

    let GetOrCreate (sessionId: string, onHostAbort: unit -> unit) : ChildSessionMailbox =
        match Map.tryFind sessionId mailboxes with
        | Some mb -> mb
        | None ->
            let mb = ChildSessionMailbox(onHostAbort)
            mailboxes <- Map.add sessionId mb mailboxes
            mb

    let Remove (sessionId: string) =
        mailboxes <- Map.remove sessionId mailboxes
