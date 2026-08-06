namespace Wanxiangshu.Process

open System
open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Session

[<RequireQualifiedAccess>]
type PtySignal =
    | Terminate
    | Kill
    | Interrupt
    | Hangup
    | Quit
    | User1
    | User2

module PtySignal =
    [<Literal>]
    let TermName = "TERM"

    [<Literal>]
    let KillName = "KILL"

    [<Literal>]
    let IntName = "INT"

    [<Literal>]
    let HupName = "HUP"

    [<Literal>]
    let QuitName = "QUIT"

    [<Literal>]
    let User1Name = "USR1"

    [<Literal>]
    let User2Name = "USR2"

    let tryParse (value: string) =
        match value with
        | TermName -> Ok PtySignal.Terminate
        | KillName -> Ok PtySignal.Kill
        | IntName -> Ok PtySignal.Interrupt
        | HupName -> Ok PtySignal.Hangup
        | QuitName -> Ok PtySignal.Quit
        | User1Name -> Ok PtySignal.User1
        | User2Name -> Ok PtySignal.User2
        | _ -> Error(sprintf "Unsupported PTY signal: %s" value)

[<RequireQualifiedAccess>]
type PtyCommand =
    | Spawn of command: string * cwd: string
    | Write of bytes: byte[]
    | Read
    | Signal of signal: PtySignal
    | Resize of width: int * height: int

type PtyId =
    | PtyId of id: string

    member this.Value =
        match this with
        | PtyId id -> id

    static member Create(id: string) = PtyId id

/// One live PTY.
///
/// `Agent` is the managed agent the forking profile selected, held as the parsed
/// `ManagedAgent` rather than a name plus a role. The previous shape had
/// `AgentId: string option` and `Role: Role option`, and `PtyPort.Fork` was
/// never called with either — so every PTY completion reported role `Executor` and
/// a rebuilt name `fast-executor`, regardless of which DevOps agent opened it.
/// Keeping name and role as one parsed value makes that disagreement unrepresentable.
type PtyHandle =
    { Id: PtyId
      Command: string
      StartedAt: DateTimeOffset
      Agent: ManagedAgent }

type PtyRead =
    { Id: PtyId
      Output: string
      Closed: bool }

/// SSOT §7 cleanup policy: owner-initiated cleanup sends TERM, waits this grace
/// window, then escalates to KILL before awaiting process exit. This is
/// resource-cleanup policy, NOT a second business deadline — the only business
/// deadline is the 3x watchdog in the orchestrator.
type PtyBackendHandler = PtyId -> PtyCommand -> Task<Result<unit, string>>

[<RequireQualifiedAccess>]
module PtyOutcome =
    [<Literal>]
    let Closed = "closed"

    [<Literal>]
    let Signalled = "signalled"

    [<Literal>]
    let termToKillGraceMs = 5000

/// Buffered-read plan: unknown id, already in-flight, already closed, or parked.
type ReadPlan =
    | Unknown of string
    | AlreadyInProgress
    | ClosedImmediate
    | Park of TaskCompletionSource<Result<string * bool, string>>
