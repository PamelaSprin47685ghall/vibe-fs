namespace Wanxiangshu.Next.Tools

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

type SessionCommandError = Wanxiangshu.Next.Session.SessionCommandError
type SessionCommandResult = Wanxiangshu.Next.Session.SessionCommandResult

type SessionCommandPort =
    abstract Request:
        command: SessionCommand ->
        cancellation: CancellationToken ->
        deadline: Deadline ->
            Task<Result<SessionCommandResult, SessionCommandError>>

type ToolContext =
    { SessionId: SessionId
      Workspace: string
      Cancellation: CancellationToken
      Deadline: Deadline
      Session: SessionCommandPort }

type ToolInput = { Payload: string }
type ToolOutput = { Result: string; Truncated: bool }

type Tool =
    { Name: string
      Description: string
      SchemaJson: string
      Execute: ToolContext -> ToolInput -> Task<ToolOutput> }

type SessionInboxCommandPort(inbox: ISessionInbox) =
    interface SessionCommandPort with
        member _.Request (command: SessionCommand) (cancellation: CancellationToken) (deadline: Deadline) =
            task {
                cancellation.ThrowIfCancellationRequested()
                let tcs = JsTcs<Result<SessionCommandResult, SessionCommandError>>()

                let cmdWithReply =
                    match command with
                    | UpsertTodo(snap, _) -> UpsertTodo(snap, (fun res -> tcs.TrySetResult(res) |> ignore))
                    | QuerySnapshot reply -> QuerySnapshot reply

                let cmdEvent = SessionCommandEvent cmdWithReply

                match inbox.TryPost cmdEvent with
                | Error _ -> return Error SessionCommandError.InboxFull
                | Ok() ->
                    use reg =
                        cancellation.Register(fun () ->
                            tcs.TrySetResult(Error(SessionCommandError.Timeout "cancelled")) |> ignore)

                    return! tcs.Task
            }
