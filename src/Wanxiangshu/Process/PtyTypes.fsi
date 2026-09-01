namespace Wanxiangshu.Process

open System
open System.Threading.Tasks
open Wanxiangshu.OpenCode

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

[<RequireQualifiedAccess>]
type PtyCommand =
    | Spawn of command: string * cwd: string
    | Write of bytes: byte array
    | Read
    | Signal of signal: PtySignal
    | Resize of width: int * height: int

type PtyId =
    | PtyId of id: string

    static member Create: id: string -> PtyId
    member Value: string

type PtyHandle =
    { Id: PtyId
      Command: string
      StartedAt: DateTimeOffset
      Agent: ManagedAgent }

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

type ReadPlan =
    | Unknown of string
    | AlreadyInProgress
    | ClosedImmediate
    | Park of TaskCompletionSource<Result<string * bool, string>>
