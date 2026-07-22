namespace Wanxiangshu.Next.Tools

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

[<RequireQualifiedAccess>]
type SessionCommandError =
    | InboxFull
    | Timeout of reason: string
    | CommandFailed of reason: string

[<RequireQualifiedAccess>]
type SessionCommandResult =
    | Upserted
    | SnapshotQueried of Fact.TodoSnapshot

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
                let cmdEvent = SessionCommandEvent command

                match inbox.TryPost cmdEvent with
                | Ok() -> return Ok SessionCommandResult.Upserted
                | Error _ -> return Error SessionCommandError.InboxFull
            }
