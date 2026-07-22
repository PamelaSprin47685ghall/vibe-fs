namespace Wanxiangshu.Next.Session

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome

type SessionCommand =
    | UpsertTodo of Fact.TodoSnapshot
    | QuerySnapshot of reply: (Fact.TodoSnapshot -> unit)

type SessionInboxEvent =
    | HumanMessageEvent of turnId: TurnId * text: string
    | PluginEvent of name: string * payload: string
    | AssistantTerminalEvent of userMessageId: MessageId * assistantMessageId: MessageId * outcome: Fact.PromptOutcome
    | ToolAfterEvent of toolName: string * callId: string * argsJson: string * outputJson: string
    | SessionCommandEvent of command: SessionCommand
    | CancelEvent of reason: string
    | LifecycleEvent of kind: string

type ISessionInbox =
    abstract TryPost: event: SessionInboxEvent -> Result<unit, SessionError>
    abstract Receive: cancellationToken: CancellationToken -> Task<SessionInboxEvent>

type FifoInbox(capacity: int) =
    let queue = new ConcurrentQueue<SessionInboxEvent>()
    let sem = new SemaphoreSlim(0, Int32.MaxValue)
    let mutable count = 0

    interface ISessionInbox with

        member _.TryPost(event: SessionInboxEvent) : Result<unit, SessionError> =
            let currentCount = Interlocked.Increment(&count)

            if currentCount > capacity then
                Interlocked.Decrement(&count) |> ignore
                Error SessionError.InboxFull
            else
                queue.Enqueue(event)
                sem.Release() |> ignore
                Ok()

        member _.Receive(cancellationToken: CancellationToken) : Task<SessionInboxEvent> =
            task {
                do! sem.WaitAsync(cancellationToken)

                match queue.TryDequeue() with
                | true, item ->
                    Interlocked.Decrement(&count) |> ignore
                    return item
                | false, _ ->
                    Interlocked.Decrement(&count) |> ignore
                    return LifecycleEvent "inbox-desync"
            }
