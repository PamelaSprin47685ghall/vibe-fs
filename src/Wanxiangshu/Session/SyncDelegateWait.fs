module SyncDelegateWait

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// EXEC-026 / EXEC-028: wait descriptors for the reusable SyncDelegate CE
/// (Acquire → GetOrCreate → Send → await Returned → await Completion).
type SyncDelegateWait =
    | ReturnFromDelegate of owner: SessionId * delegateSession: SessionId * role: SyncDelegateRole
    | DelegateCompletionTerminal of
        owner: SessionId *
        delegateSession: SessionId *
        role: SyncDelegateRole *
        toolRun: ProviderRunIdentity

/// Diagnostic wait descriptor for the two SyncDelegate CE await points.
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
    | ReturnFromDelegate(owner, delegateSession, role) ->
        DiagnosticWait.create
            "sync-delegate-return"
            (toolOwner owner)
            [ "owner", SessionId.value owner
              "delegate", SessionId.value delegateSession
              "role", roleLabel role ]
            (delegateProducer delegateSession)
            [ cancelEscape owner; WaitEscape.SessionLifetime ]
            "SyncDelegateRuntime.Invoke"

    | DelegateCompletionTerminal(owner, delegateSession, role, toolRun) ->
        DiagnosticWait.create
            "sync-delegate-completion"
            (toolOwner owner)
            [ "owner", SessionId.value owner
              "delegate", SessionId.value delegateSession
              "role", roleLabel role
              "tool_run", ProviderRunIdentity.value toolRun ]
            (delegateProducer delegateSession)
            [ cancelEscape owner; WaitEscape.SessionLifetime ]
            "SyncDelegateRuntime.Invoke"
