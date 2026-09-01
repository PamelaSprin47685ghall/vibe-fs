namespace Wanxiangshu.Process

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Session
open Wanxiangshu.OpenCode

type PtyPort =
    new:
        ?mailboxSender: (PtyJoinItem -> unit) *
        ?handler: PtyBackendHandler *
        ?agentProvider: (unit -> AgentRecord list) ->
            PtyPort

    member AddMailboxSender: sender: (PtyJoinItem -> unit) -> unit
    member Close: id: PtyId * ?outcome: Result<string, string> -> unit
    member CloseAll: ?graceMs: int -> Task<unit>
    member Complete: id: PtyId * ?outcome: Result<string, string> -> unit
    member CompleteAborted: id: PtyId * ?message: string -> unit
    member Exists: id: PtyId -> bool
    member FailRead: id: PtyId * reason: string -> unit
    member Fork: command: string * agent: ManagedAgent * ?ptyId: PtyId * ?cwd: string -> PtyId
    member Known: id: PtyId -> bool
    member List: unit -> AgentRecord list * PtyHandle list
    member Read: id: PtyId -> Task<Result<string * bool, string>>
    member ReadResult: id: PtyId * output: string * closed: bool -> unit
    member RegisterExitTask: id: PtyId * task: Task -> unit
    member Send: id: PtyId * command: PtyCommand -> Task<Result<unit, string>>
    member AgentProvider: (unit -> AgentRecord list)
    member Handler: PtyBackendHandler
    member MailboxSender: (PtyJoinItem -> unit) option
