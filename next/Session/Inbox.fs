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

type ISessionInbox =
    abstract TryPost: event: SessionInboxEvent -> Result<unit, SessionError>
    abstract Receive: cancellationToken: CancellationToken -> Task<SessionInboxEvent>

type FifoInbox(capacity: int) =
    let queue = System.Collections.Generic.Queue<SessionInboxEvent>()

    let waiters = System.Collections.Generic.Queue<JsTcs<SessionInboxEvent>>()

    let lockObj = obj ()

    interface ISessionInbox with

        member _.TryPost(event: SessionInboxEvent) : Result<unit, SessionError> =
            lock lockObj (fun () ->
                if waiters.Count > 0 then
                    let waiter = waiters.Dequeue()
                    waiter.TrySetResult(event) |> ignore
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
                            let tcs = JsTcs<SessionInboxEvent>()
                            waiters.Enqueue(tcs)
                            (None, Some tcs))

                match itemOpt, waiterOpt with
                | Some item, _ -> return item
                | None, Some tcs ->
                    use reg =
                        cancellationToken.Register(fun () ->
                            tcs.TrySetResult(LifecycleEvent "inbox-cancelled") |> ignore)

                    return! tcs.Task
                | None, None -> return LifecycleEvent "inbox-desync"
            }
