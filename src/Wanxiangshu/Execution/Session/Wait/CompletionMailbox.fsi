namespace Wanxiangshu.Execution.Session.Wait

open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity

module JoinBatch =
    val Max: int
    val MaxJoinBatch: int

type NonEmptyBatch<'item> = private NonEmptyBatch of head: 'item * tail: 'item list

module NonEmptyBatch =
    val ofHeadTail: head: 'item -> tail: 'item list -> NonEmptyBatch<'item>
    val tryOfList: ('item list -> NonEmptyBatch<'item> option)
    val toList: NonEmptyBatch<'item> -> 'item list
    val length: NonEmptyBatch<'item> -> int
    val map: f: ('a -> 'b) -> NonEmptyBatch<'a> -> NonEmptyBatch<'b>

[<RequireQualifiedAccess>]
type JoinInterruptReason =
    | OperatorAbort
    | UserMessageArrived
    | DeadlineExpired

type JoinWaitOutcome<'item> =
    | ResultsAvailable of NonEmptyBatch<'item>
    | Interrupted of JoinInterruptReason

type MailboxWakeReason =
    | CompletionMayBeAvailable
    | LocalInterrupt of JoinInterruptReason
    | MailboxCancelled

type JoinInterrupt =
    { Wait: Task<JoinInterruptReason>
      Signal: JoinInterruptReason -> unit }

module JoinInterrupt =
    val create: unit -> JoinInterrupt

type CompletionMailbox =
    new: gate: obj -> CompletionMailbox
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
