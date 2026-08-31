namespace Wanxiangshu.Execution.Delegation.Handle.OpenCode

open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
module JoinWake =

    /// EXEC-017: only a real external user material interrupts active joins.
    /// Plugin-owned continuations and Host compaction are not external-user
    /// arrivals. The registry itself remains attempt-scoped and drops the wake
    /// when no attempt is active.
    let observeChatMessage (registry: IJoinAttemptRegistry) (intent: ChatAdmissionIntent.Decision) =
        match intent with
        | ChatAdmissionIntent.Decision.ExternalRootIntent evidence -> registry.SignalUserMessage evidence.Key.SessionId
        | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent evidence ->
            registry.SignalUserMessage evidence.Key.SessionId
        | ChatAdmissionIntent.Decision.NoManagedExecution _
        | ChatAdmissionIntent.Decision.PendingPromptIntent _
        | ChatAdmissionIntent.Decision.HostInternal _
        | ChatAdmissionIntent.Decision.Reject _ -> ()
