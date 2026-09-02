namespace Wanxiangshu.Execution.Delegation.Handle

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// Attempt-scoped join interrupt registry (EXEC-017).
///
/// EXEC-017 semantics: an external user message interrupts ONLY the current
/// active JoinAttempt. There is NO session-level future latch: a signal that
/// arrives with zero active attempts is dropped as a join wake (the user
/// message itself still stays in the normal Host queue — dropping only means
/// we do not generate a join interruption for it).
///
/// `Begin(session, ?toolCall)` opens a per-attempt attempt; `SignalUserMessage`
/// fans `UserMessageArrived` to every active attempt of the session and drops
/// when none are active. `OperatorAbort` / `DeadlineExpired` are driven through
/// the lease (JoinTool / timeout), not by the registry.
type JoinAttemptLease(interrupt: JoinInterrupt, unregister: unit -> unit) =

    member _.Wait: Task<JoinInterruptReason> = interrupt.Wait

    member _.SignalOperatorAbort() =
        interrupt.Signal JoinInterruptReason.OperatorAbort

    member _.SignalUserMessage() =
        interrupt.Signal JoinInterruptReason.UserMessageArrived

    member _.SignalDeadline() =
        interrupt.Signal JoinInterruptReason.DeadlineExpired

    interface IDisposable with
        member _.Dispose() = unregister ()

type IJoinAttemptRegistry =
    abstract Begin: SessionId * ToolCallId option -> JoinAttemptLease
    /// External user message arrived for a session. Wakes every ACTIVE attempt;
    /// zero active attempts → drop as a join wake (no future latch).
    abstract SignalUserMessage: SessionId -> unit
    /// Drop active attempts for a deleted session. Does not signal.
    abstract ClearSession: SessionId -> unit

/// Thread-safe process-local attempt registry (Dictionary + lock).
type JoinAttemptRegistry() =
    let gate = obj ()
    /// DSL-cross-callback-proof: physical waiter — live JoinAttemptLease wait handles only
    // DSL-MUTABLE: resource — active join attempt registry by session key
    let active = Dictionary<string, ResizeArray<JoinAttemptLease>>()

    let removeLease (key: string) (list: ResizeArray<JoinAttemptLease>) (lease: JoinAttemptLease) =
        list.Remove lease |> ignore

        if list.Count = 0 then
            active.Remove key |> ignore

    let unregister (key: string) (lease: JoinAttemptLease) =
        lock gate (fun () ->
            match active.TryGetValue key with
            | true, list -> removeLease key list lease
            | false, _ -> ())

    interface IJoinAttemptRegistry with
        member _.Begin(sessionId: SessionId, _toolCall: ToolCallId option) : JoinAttemptLease =
            let key = SessionId.value sessionId
            let interrupt = JoinInterrupt.create ()
            // A mutable cell lets the dispose closure reference the lease without a
            // recursive-object construction (avoids F# warning 40 / TreatWarningsAsErrors).
            // DSL-MUTABLE: resource — lease self-reference cell for dispose closure
            let leaseRef = ref Unchecked.defaultof<JoinAttemptLease>

            let lease =
                new JoinAttemptLease(interrupt, (fun () -> unregister key leaseRef.Value))

            leaseRef.Value <- lease

            lock gate (fun () ->
                match active.TryGetValue key with
                | true, list -> list.Add lease
                | false, _ ->
                    // DSL-MUTABLE: algorithm-scratch — new lease list for dictionary insert
                    let list = ResizeArray<JoinAttemptLease>()
                    list.Add lease
                    active.[key] <- list)

            lease

        member _.SignalUserMessage(sessionId: SessionId) : unit =
            let key = SessionId.value sessionId

            let attempts =
                lock gate (fun () ->
                    match active.TryGetValue key with
                    | true, list when list.Count > 0 -> list |> Seq.toList
                    | _ -> [])

            // Only the CURRENT active attempts wake; none active → dropped.
            for attempt in attempts do
                attempt.SignalUserMessage()

        member _.ClearSession(sessionId: SessionId) : unit =
            let key = SessionId.value sessionId
            lock gate (fun () -> active.Remove key |> ignore)
