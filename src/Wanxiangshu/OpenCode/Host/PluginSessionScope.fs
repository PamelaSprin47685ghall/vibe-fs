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
    // DSL-MUTABLE: resource — identities retained through staged Inspector finalization.
    let retainedSessionIdentities = HashSet<string>()

    // HOST-012: 跨实例共享（模块级单例）——worktree 独立插件实例的 fork→verdict
    // 链必须读写同一份。每实例独有状态（OwnedSessions、UserMessageBindings、
    // Companions 等）保持 per-instance。
    // DSL-MUTABLE: resource — alias to SharedState session directory map.
    member val SessionDirectories = SharedState.SessionDirectories
    // DSL-MUTABLE: resource — per-instance owned session set.
    member val OwnedSessions = HashSet<string>()
    /// EMR-004: per-plugin-instance routing demands, including a root chat.message
    /// that may block before PromptIngress has had a chance to register ownership.
    /// This is cleanup bookkeeping only, never business/session authority.
    // DSL-MUTABLE: resource — per-instance routing session set.
    member val ModelRoutingSessions = HashSet<string>()
    // DSL-MUTABLE: resource — per-instance user message binding map.
    member val UserMessageBindings = Dictionary<string, PhysicalUserMessageId>()
    // DSL-MUTABLE: resource — alias to SharedState session parent map.
    member val SessionParents = SharedState.SessionParents
    // DSL-MUTABLE: resource — per-instance companion registry.
    member val Companions = Dictionary<string, CompanionHost>()
    // DSL-MUTABLE: resource — lock gate object for companion operations.
    member val CompanionGate = obj ()
    // DSL-MUTABLE: resource — alias to SharedState verdict session set.
    member val VerdictSubmissions = SharedState.VerdictSubmissions
    // DSL-MUTABLE: single-flight — per-instance nudge sent set.
    member val NudgeSent = HashSet<string>()
    // DSL-MUTABLE: single-flight — per-instance join guard nudge set.
    member val JoinGuardNudges = HashSet<string>()
    // DSL-MUTABLE: resource — per-instance aborted session set.
    member val AbortedSessions = HashSet<string>()
    // HOST-004: process-local idle-derived continuation admission. Per plugin
    // instance like NudgeSent / LoopSensor; never journalled (HOST-007). A
    // worktree owner transfer starts a fresh gate — no old permit survives.
    // DSL-MUTABLE: resource — per-instance quiescence gate instance.
    member val Quiescence = SessionQuiescenceGate()
    /// EXEC-017: process-local attempt-scoped join registry. External user messages
    /// signal only the CURRENT active JoinAttempt (UserMessageArrived), without
    /// cancelling mailbox/runtime and without a future latch (not journaled).
    // DSL-MUTABLE: resource — per-instance join interrupt registry.
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

    member _.DropSessionIdentity(sessionId: string) =
        retainedSessionIdentities.Remove sessionId |> ignore
        let sid = SessionId.create sessionId
        SessionProviderLanguage.drop sid
        SessionPersona.drop sid

    /// Session deletion drops every per-instance registry entry for this
    /// session (mirror of DisposeSession's per-session cleanup).
    member this.ClearSession(sessionId: string, preserveIdentity: bool) =
        match this.Companions.TryGetValue sessionId with
        | true, companion ->
            this.Companions.Remove sessionId |> ignore
            (companion :> IDisposable).Dispose()
        | false, _ -> ()

        this.OwnedSessions.Remove sessionId |> ignore
        this.ModelRoutingSessions.Remove sessionId |> ignore
        this.UserMessageBindings.Remove sessionId |> ignore
        this.SessionParents.Remove sessionId |> ignore
        SessionExecutionBinding.drop (SessionId.create sessionId)
        this.SessionDirectories.Remove sessionId |> ignore
        let sid = SessionId.create sessionId

        this.VerdictSubmissions
        |> Seq.filter (JudgementRequestIdentity.belongsTo sid)
        |> Seq.toArray
        |> Array.iter (fun request -> this.VerdictSubmissions.Remove request |> ignore)

        this.AbortedSessions.Remove sessionId |> ignore

        if preserveIdentity then
            retainedSessionIdentities.Add sessionId |> ignore
        else
            this.DropSessionIdentity sessionId

        // HOST-004 Q-10: a deleted session's idle permits die forever.
        this.Quiescence.DropSession sid
        // SessionDeleted: drop join-interrupt waiters + one-shot user-message latch.
        this.JoinInterrupts.ClearSession sid

    /// Plugin dispose releases every companion host and every routing demand/lease
    /// this instance touched. The process-shared allocator remains alive for sibling
    /// root/worktree plugin instances.
    member this.Dispose() =
        for companion in this.Companions.Values |> Seq.toList do
            (companion :> IDisposable).Dispose()

        this.Companions.Clear()

        for sessionId in retainedSessionIdentities |> Seq.toArray do
            this.DropSessionIdentity sessionId

        let routed =
            Seq.append this.ModelRoutingSessions this.OwnedSessions
            |> Seq.distinct
            |> Seq.toArray

        for sessionId in routed do
            SessionExecutionBinding.drop (SessionId.create sessionId)

        this.ModelRoutingSessions.Clear()
        this.OwnedSessions.Clear()
