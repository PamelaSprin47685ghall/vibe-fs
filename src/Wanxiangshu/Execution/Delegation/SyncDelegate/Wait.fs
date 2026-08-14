namespace Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
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

module SyncDelegateWait

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// EXEC-026 / EXEC-031: wait descriptors for SyncDelegate CE
/// (Acquire → GetOrCreate → Send → await ordinary Completion / WorkRecord).
type SyncDelegateWait = DelegateCompletion of owner: SessionId * delegateSession: SessionId * role: SyncDelegateRole

/// Diagnostic wait descriptor for the SyncDelegate CE await point.
let describe (wait: SyncDelegateWait) : DiagnosticWait =
    let roleLabel = SyncDelegate.roleLabel

    let toolOwner (owner: SessionId) =
        CausalOwner.create "sync-delegate-tool" [ "session", SessionId.value owner ]

    let delegateProducer (delegateSession: SessionId) =
        WorkflowProducer(
            CausalOwner.create "sync-delegate-session-workflow" [ "session", SessionId.value delegateSession ]
        )

    let cancelEscape (owner: SessionId) =
        WaitEscape.CancelledBy(CausalOwner.create "owner-session" [ "session", SessionId.value owner ])

    match wait with
    | DelegateCompletion(owner, delegateSession, role) ->
        DiagnosticWait.create
            "sync-delegate-completion"
            (toolOwner owner)
            [ "owner", SessionId.value owner
              "delegate", SessionId.value delegateSession
              "role", roleLabel role ]
            (delegateProducer delegateSession)
            [ cancelEscape owner; WaitEscape.SessionLifetime ]
            "SyncDelegateRuntime.Invoke"
