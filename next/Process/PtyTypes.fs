namespace Wanxiangshu.Next.Process

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Session

[<RequireQualifiedAccess>]
type PtySignal =
    | Terminate
    | Kill
    | Interrupt

module PtySignal =
    [<Literal>]
    let TermName = "TERM"

    [<Literal>]
    let KillName = "KILL"

    let tryParse (value: string) =
        match value with
        | TermName -> Ok PtySignal.Terminate
        | KillName -> Ok PtySignal.Kill
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

type PtyHandle =
    { Id: PtyId
      Command: string
      StartedAt: DateTimeOffset
      AgentId: string option
      Role: AgentRole option }

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
    let termToKillGraceMs = 500

/// Buffered-read plan: unknown id, already in-flight, already closed, or parked.
type ReadPlan =
    | Unknown of string
    | AlreadyInProgress
    | ClosedImmediate
    | Park of TaskCompletionSource<Result<string * bool, string>>
