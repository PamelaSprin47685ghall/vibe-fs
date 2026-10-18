namespace Wanxiangshu.Process

open System
open System.Threading.Tasks

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
    val TermName: string = "TERM"

    [<Literal>]
    val KillName: string = "KILL"

    [<Literal>]
    val IntName: string = "INT"

    [<Literal>]
    val HupName: string = "HUP"

    [<Literal>]
    val QuitName: string = "QUIT"

    [<Literal>]
    val User1Name: string = "USR1"

    [<Literal>]
    val User2Name: string = "USR2"

    val tryParse: value: string -> Result<PtySignal, string>

type PtyRecoveryBinding =
    { Directory: string
      Key: string
      IncarnationId: string }

[<RequireQualifiedAccess>]
type PtyRecoveryExit =
    | Ended of incarnationId: string * stopped: bool * code: int option * signal: string option
    | NotStarted of incarnationId: string * reason: string

type RecoverPty = string -> string -> string option -> int -> Task<PtyRecoveryExit>

[<RequireQualifiedAccess>]
type PtyCommand =
    | Spawn of command: string * cwd: string * recovery: PtyRecoveryBinding option
    | Write of bytes: byte array
    | Read
    | Signal of signal: PtySignal
    | Resize of width: int * height: int

type PtyId =
    | PtyId of id: string

    static member Create: id: string -> PtyId
    member Value: string

/// Confirmed PTY exit or explicit no-start evidence delivered to listeners.
[<RequireQualifiedAccess>]
type PtyExitEvent =
    | Exited of id: PtyId * outcome: string
    | Failed of id: PtyId * code: string * message: string
    | Aborted of id: PtyId * code: string * message: string
    | NotStarted of id: PtyId * reason: string

    member Id: PtyId

type PtyHandle =
    { Id: PtyId
      Command: string
      StartedAt: DateTimeOffset
      Agent: string }

type PtyRead =
    { Id: PtyId
      Output: string
      Closed: bool }

type PtyBackendHandler = PtyId -> PtyCommand -> Task<Result<unit, string>>

[<RequireQualifiedAccess>]
module PtyOutcome =
    [<Literal>]
    val Closed: string = "closed"

    [<Literal>]
    val termToKillGraceMs: int = 5000
