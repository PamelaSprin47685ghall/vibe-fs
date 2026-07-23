namespace Wanxiangshu.Next.Session

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome

[<RequireQualifiedAccess>]
type SessionCommandError =
    | InboxFull
    | Timeout of reason: string
    | CommandFailed of reason: string

[<RequireQualifiedAccess>]
type SessionCommandResult =
    | Upserted
    | SnapshotQueried of Fact.TodoSnapshot
    | ReviewSubmitted
    | VerdictReturned

type SessionCommand =
    | UpsertTodo of Fact.TodoSnapshot * reply: (Result<SessionCommandResult, SessionCommandError> -> unit)
    | QuerySnapshot of reply: (Fact.TodoSnapshot -> unit)
    | SubmitReview of report: string * reply: (Result<SessionCommandResult, SessionCommandError> -> unit)
    | ReturnVerdict of verdict: string * reply: (Result<SessionCommandResult, SessionCommandError> -> unit)

type SessionInboxEvent =
    | HumanMessageEvent of turnId: TurnId * text: string
    | PluginEvent of name: string * payload: string
    | AssistantTerminalEvent of userMessageId: MessageId * assistantMessageId: MessageId * outcome: Fact.PromptOutcome
    | ToolAfterEvent of toolName: string * callId: string * argsJson: string * outputJson: string
    | SessionCommandEvent of command: SessionCommand
    | CancelEvent of reason: string
    | LifecycleEvent of kind: string
    | LoopCommandEvent of sessionId: SessionId * taskText: string
    | SquadCommandEvent of squadId: string * actionText: string

type private InboxWaiter =
    { Task: Task<SessionInboxEvent>
      Resolve: SessionInboxEvent -> unit
      Reject: exn -> unit }

type ISessionInbox =
    abstract TryPost: event: SessionInboxEvent -> Result<unit, SessionError>
    abstract Receive: cancellationToken: CancellationToken -> Task<SessionInboxEvent>

type FifoInbox(capacity: int) =
    let queue = System.Collections.Generic.Queue<SessionInboxEvent>()

    let waiters = System.Collections.Generic.Queue<InboxWaiter>()

    let lockObj = obj ()

    let removeWaiter (waiter: InboxWaiter) =
        let remaining = System.Collections.Generic.Queue<InboxWaiter>()

        while waiters.Count > 0 do
            let candidate = waiters.Dequeue()

            if not (obj.ReferenceEquals(candidate, waiter)) then
                remaining.Enqueue(candidate)

        while remaining.Count > 0 do
            waiters.Enqueue(remaining.Dequeue())

    let cancelWaiter (waiter: InboxWaiter) =
        lock lockObj (fun () -> removeWaiter waiter)
        waiter.Reject(OperationCanceledException("Session inbox receive cancelled"))

    interface ISessionInbox with

        member _.TryPost(event: SessionInboxEvent) : Result<unit, SessionError> =
            lock lockObj (fun () ->
                if waiters.Count > 0 then
                    let waiter = waiters.Dequeue()
                    waiter.Resolve event
                    Ok()
                elif queue.Count >= capacity then
                    Error SessionError.InboxFull
                else
                    queue.Enqueue(event)
                    Ok())

        member _.Receive(cancellationToken: CancellationToken) : Task<SessionInboxEvent> =
            task {
                cancellationToken.ThrowIfCancellationRequested()

                let itemOpt, waiterOpt =
                    lock lockObj (fun () ->
                        if queue.Count > 0 then
                            (Some(queue.Dequeue()), None)
                        else
                            let mutable resolveFn: (SessionInboxEvent -> unit) option = None
                            let mutable rejectFn: (exn -> unit) option = None

                            let promise =
                                Fable.Core.JS.Constructors.Promise.Create(fun resolve reject ->
                                    resolveFn <- Some resolve
                                    rejectFn <- Some reject)

                            let waiter =
                                { Task = unbox promise
                                  Resolve = fun event -> resolveFn |> Option.iter (fun resolve -> resolve event)
                                  Reject = fun error -> rejectFn |> Option.iter (fun reject -> reject error) }

                            waiters.Enqueue(waiter)
                            (None, Some waiter))

                match itemOpt, waiterOpt with
                | Some item, _ -> return item
                | None, Some waiter ->
                    use reg = cancellationToken.Register(fun () -> cancelWaiter waiter)

                    return! waiter.Task
                | None, None -> return LifecycleEvent "inbox-desync"
            }
