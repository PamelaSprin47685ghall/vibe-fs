namespace Wanxiangshu.Execution.Delegation.Fork

open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Dependency-inverted process capability behind the public ForkRuntime shell.
/// The implementation lives in the owner-local runtime project; foreign owners
/// compile only this contract and therefore never source-merge mutable registries.
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

type private UnavailableForkRuntimeBackend() =
    interface IForkRuntimeBackend with
        member _.Fork(agentId, _, _, _, _) = ForkResult.NotFound agentId
        member _.WaitForSignal _ = Task.FromResult MailboxWakeReason.MailboxCancelled
        member _.WaitForWake() = Task.FromResult MailboxWakeReason.MailboxCancelled
        member _.PulseWake() = ()
        member _.PulseAgentHandle _ = ()
        member _.PublishPtyCompletion _ = ()
        member _.DrainAgentWakes _ = []
        member _.DrainPtyCompletions _ = []
        member _.RegisterPty _ = ()
        member _.UnregisterPty _ = ()
        member _.Restore(_, _, _) = ()
        member _.MarkInterrupted(_, _) = ()
        member _.BindChildSession(_, _) = ()
        member _.AwaitAgent(agentId, _) = Task.FromResult(Error(sprintf "ForkRuntime backend unavailable: %s" agentId))
        member _.CancelAgent _ = ()
        member _.List() = [], []
        member _.IsCancelled = true
        member _.ActiveRunCount = 0
        member _.PendingCompletionCount = 0
        member _.PendingPtyCount = 0
        member _.Cancel() = ()

/// Foreign-safe capability shell. All process-local state belongs to the backend
/// supplied by delegation runtime composition; the default is deliberately inert.
[<Sealed>]
type ForkRuntime(?backend: IForkRuntimeBackend) =
    let backend = defaultArg backend (UnavailableForkRuntimeBackend() :> IForkRuntimeBackend)

    member _.Fork
        (agentId: string, role: Role, agent: string, ?prompt: string, ?runWork: unit -> Task<AgentCompletionOutcome>)
        : ForkResult =
        backend.Fork(agentId, role, agent, prompt, runWork)

    member _.WaitForSignal(interrupt: Task<JoinInterruptReason>) = backend.WaitForSignal interrupt
    member _.WaitForWake() = backend.WaitForWake()
    member _.PulseWake() = backend.PulseWake()
    member _.PulseAgentHandle(handle: AgentHandleId) = backend.PulseAgentHandle handle
    member _.PublishPtyCompletion(item: PtyJoinItem) = backend.PublishPtyCompletion item
    member _.DrainAgentWakes(maxCount: int) = backend.DrainAgentWakes maxCount
    member _.DrainPtyCompletions(maxCount: int) = backend.DrainPtyCompletions maxCount
    member _.RegisterPty(pty: PtyRecord) = backend.RegisterPty pty
    member _.UnregisterPty(ptyId: string) = backend.UnregisterPty ptyId
    member _.Restore(agentId: string, role: Role, agent: string) = backend.Restore(agentId, role, agent)
    member _.MarkInterrupted(agentId: string, reason: string) = backend.MarkInterrupted(agentId, reason)
    member _.BindChildSession(agentId: string, childSessionId: SessionId) = backend.BindChildSession(agentId, childSessionId)
    member _.AwaitAgent(agentId: string, ?timeoutMs: int) = backend.AwaitAgent(agentId, timeoutMs)
    member _.CancelAgent(agentId: string) = backend.CancelAgent agentId
    member _.List() = backend.List()
    member _.IsCancelled = backend.IsCancelled
    member _.ActiveRunCount = backend.ActiveRunCount
    member _.PendingCompletionCount = backend.PendingCompletionCount
    member _.PendingPtyCount = backend.PendingPtyCount
    member _.Cancel() = backend.Cancel()
    member this.Close() = this.Cancel()
