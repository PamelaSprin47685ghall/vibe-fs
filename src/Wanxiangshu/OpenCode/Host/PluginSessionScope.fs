namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Per-instance session registry state for one plugin instance (HOST-012):
/// owned sessions, user-message bindings, companions, verdicts, nudges,
/// quiescence permits and join interrupts. Shared cross-worktree state stays
/// in SharedState; everything here is per-instance and dies with the scope.
type PluginSessionScope() =
    // HOST-012: 跨实例共享（模块级单例）——worktree 独立插件实例的 fork→verdict
    // 链必须读写同一份。每实例独有状态（OwnedSessions、UserMessageBindings、
    // Companions 等）保持 per-instance。
    member val SessionDirectories = SharedState.SessionDirectories
    member val OwnedSessions = HashSet<string>()
    member val UserMessageBindings = Dictionary<string, PhysicalUserMessageId>()
    member val SessionParents = SharedState.SessionParents
    member val Companions = Dictionary<string, CompanionHost>()
    member val CompanionGate = obj ()
    member val VerdictSessions = SharedState.VerdictSessions
    member val NudgeSent = HashSet<string>()
    member val JoinGuardNudges = HashSet<string>()
    member val AbortedSessions = HashSet<string>()
    // HOST-004: process-local idle-derived continuation admission. Per plugin
    // instance like NudgeSent / LoopSensor; never journalled (HOST-007). A
    // worktree owner transfer starts a fresh gate — no old permit survives.
    member val Quiescence = SessionQuiescenceGate()
    /// EXEC-017: process-local attempt-scoped join registry. External user messages
    /// signal only the CURRENT active JoinAttempt (UserMessageArrived), without
    /// cancelling mailbox/runtime and without a future latch (not journaled).
    member val JoinInterrupts: IJoinAttemptRegistry = JoinAttemptRegistry() :> IJoinAttemptRegistry

    /// C6 item 27: waiters are keyed by BloggerSessionId. When the MAIN is
    /// deleted, the linked Blogger's parked waiter + request slots must be
    /// cancelled too. Returns the keys to cancel (including sessionId itself
    /// when it is not a Main with a linked Blogger).
    member this.LinkedBloggerKeys(sessionId: string) : string list =
        let bloggerKeys (companion: CompanionHost) =
            match companion.BloggerSession with
            | Some bloggerId -> [ SessionId.value bloggerId ]
            | None -> []

        match this.Companions.TryGetValue sessionId with
        | true, companion -> bloggerKeys companion
        | false, _ ->
            // sessionId may itself be a Blogger child being deleted.
            [ sessionId ]

    /// Session deletion drops every per-instance registry entry for this
    /// session (mirror of DisposeSession's per-session cleanup).
    member this.ClearSession(sessionId: string) =
        match this.Companions.TryGetValue sessionId with
        | true, companion ->
            this.Companions.Remove sessionId |> ignore
            (companion :> IDisposable).Dispose()
        | false, _ -> ()

        this.OwnedSessions.Remove sessionId |> ignore
        this.UserMessageBindings.Remove sessionId |> ignore
        this.SessionParents.Remove sessionId |> ignore
        SessionExecutionBinding.drop (SessionId.create sessionId)
        this.SessionDirectories.Remove sessionId |> ignore
        this.VerdictSessions.Remove sessionId |> ignore
        this.AbortedSessions.Remove sessionId |> ignore
        let sid = SessionId.create sessionId
        SessionProviderLanguage.drop sid
        SessionPersona.drop sid
        // HOST-004 Q-10: a deleted session's idle permits die forever.
        this.Quiescence.DropSession sid
        // SessionDeleted: drop join-interrupt waiters + one-shot user-message latch.
        this.JoinInterrupts.ClearSession sid

    /// Plugin dispose releases every companion host.
    member this.Dispose() =
        for companion in this.Companions.Values |> Seq.toList do
            (companion :> IDisposable).Dispose()

        this.Companions.Clear()
