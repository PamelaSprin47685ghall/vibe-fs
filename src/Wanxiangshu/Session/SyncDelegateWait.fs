module SyncDelegateWait

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// EXEC-026 / EXEC-031: wait descriptors for SyncDelegate CE
/// (Acquire → GetOrCreate → Send → await ordinary Completion / WorkRecord).
type SyncDelegateWait = DelegateCompletion of owner: SessionId * delegateSession: SessionId * role: SyncDelegateRole

/// Diagnostic wait descriptor for the SyncDelegate CE await point.
let describe (wait: SyncDelegateWait) : DiagnosticWait =
    let roleLabel =
        function
        | SyncDelegateRole.Inspector -> "inspector"
        | SyncDelegateRole.Coder -> "coder"

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
