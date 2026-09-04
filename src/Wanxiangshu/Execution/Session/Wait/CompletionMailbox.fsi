namespace Wanxiangshu.Execution.Session.Wait

open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity

type CompletionMailbox =
    new: gate: obj -> CompletionMailbox
    interface ICompletionMailbox<AgentHandleId, PtyJoinItem, JoinInterruptReason, MailboxWakeReason>
    member PulseAgentHandle: handle: AgentHandleId -> unit
    member PublishPtyCompletion: item: PtyJoinItem -> unit
    member PulseWake: unit -> unit
    member WaitForWake: unit -> Task<MailboxWakeReason>
    member WaitForSignal: interrupt: Task<JoinInterruptReason> -> Task<MailboxWakeReason>
    member DrainAgentWakes: maxCount: int -> AgentHandleId list
    member DrainPtyCompletions: maxCount: int -> PtyJoinItem list
    member Cancel: unit -> bool
    member PendingCount: int
    member PendingPtyCount: int
    member PendingAgentWakeCount: int
    member IsCancelled: bool

module CompletionMailboxRuntime =
    val create:
        gate: obj -> ICompletionMailbox<AgentHandleId, PtyJoinItem, JoinInterruptReason, MailboxWakeReason>
