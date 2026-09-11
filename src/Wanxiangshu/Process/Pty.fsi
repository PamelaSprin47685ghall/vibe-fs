namespace Wanxiangshu.Process

open System.Threading.Tasks

type PtyPort =
    new:
        ?exitListener: (PtyExitEvent -> unit) * ?handler: PtyBackendHandler ->
            PtyPort

    member AddExitListener: listener: (PtyExitEvent -> unit) -> unit
    member Close: id: PtyId * ?outcome: Result<string, string> -> unit
    member CloseAll: ?graceMs: int -> Task<unit>
    member Complete: id: PtyId * ?outcome: Result<string, string> -> unit
    member CompleteAborted: id: PtyId * ?message: string -> unit
    member Exists: id: PtyId -> bool
    member FailRead: id: PtyId * reason: string -> unit
    member Fork: command: string * agentName: string * ?ptyId: PtyId * ?cwd: string -> PtyId
    member Known: id: PtyId -> bool
    member List: unit -> PtyHandle list
    member Read: id: PtyId -> Task<Result<string * bool, string>>
    member ReadResult: id: PtyId * output: string * closed: bool -> unit
    member RegisterExitTask: id: PtyId * task: Task -> unit
    member Send: id: PtyId * command: PtyCommand -> Task<Result<unit, string>>
    member Handler: PtyBackendHandler
    member ExitListener: (PtyExitEvent -> unit) option
