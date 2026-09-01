namespace Wanxiangshu.Execution.Delegation.Fork

open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type IForkRuntimeBackend =
    abstract Fork:
        agentId: string *
        role: Role *
        agent: string *
        prompt: string option *
        runWork: (unit -> Task<AgentCompletionOutcome>) option ->
            ForkResult

    abstract WaitForSignal: interrupt: Task<JoinInterruptReason> -> Task<MailboxWakeReason>
    abstract WaitForWake: unit -> Task<MailboxWakeReason>
    abstract PulseWake: unit -> unit
    abstract PulseAgentHandle: handle: AgentHandleId -> unit
    abstract PublishPtyCompletion: item: PtyJoinItem -> unit
    abstract DrainAgentWakes: maxCount: int -> AgentHandleId list
    abstract DrainPtyCompletions: maxCount: int -> PtyJoinItem list
    abstract RegisterPty: pty: PtyRecord -> unit
    abstract UnregisterPty: ptyId: string -> unit
    abstract Restore: agentId: string * role: Role * agent: string -> unit
    abstract MarkInterrupted: agentId: string * reason: string -> unit
    abstract BindChildSession: agentId: string * childSessionId: SessionId -> unit
    abstract AwaitAgent: agentId: string * timeoutMs: int option -> Task<Result<RunCompletion, string>>
    abstract CancelAgent: agentId: string -> unit
    abstract List: unit -> AgentRecord list * PtyRecord list
    abstract IsCancelled: bool
    abstract ActiveRunCount: int
    abstract PendingCompletionCount: int
    abstract PendingPtyCount: int
    abstract Cancel: unit -> unit

[<Sealed>]
type ForkRuntime =
    new: ?backend: IForkRuntimeBackend -> ForkRuntime

    member Fork:
        agentId: string *
        role: Role *
        agent: string *
        ?prompt: string *
        ?runWork: (unit -> Task<AgentCompletionOutcome>) ->
            ForkResult

    member WaitForSignal: interrupt: Task<JoinInterruptReason> -> Task<MailboxWakeReason>
    member WaitForWake: unit -> Task<MailboxWakeReason>
    member PulseWake: unit -> unit
    member PulseAgentHandle: handle: AgentHandleId -> unit
    member PublishPtyCompletion: item: PtyJoinItem -> unit
    member DrainAgentWakes: maxCount: int -> AgentHandleId list
    member DrainPtyCompletions: maxCount: int -> PtyJoinItem list
    member RegisterPty: pty: PtyRecord -> unit
    member UnregisterPty: ptyId: string -> unit
    member Restore: agentId: string * role: Role * agent: string -> unit
    member MarkInterrupted: agentId: string * reason: string -> unit
    member BindChildSession: agentId: string * childSessionId: SessionId -> unit
    member AwaitAgent: agentId: string * ?timeoutMs: int -> Task<Result<RunCompletion, string>>
    member CancelAgent: agentId: string -> unit
    member List: unit -> AgentRecord list * PtyRecord list
    member IsCancelled: bool
    member ActiveRunCount: int
    member PendingCompletionCount: int
    member PendingPtyCount: int
    member Cancel: unit -> unit
    member Close: unit -> unit
