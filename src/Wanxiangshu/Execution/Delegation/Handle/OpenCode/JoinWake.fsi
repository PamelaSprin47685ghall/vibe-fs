namespace Wanxiangshu.Execution.Delegation.Handle.OpenCode

open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
module JoinWake =

    /// EXEC-017: only a real external user material interrupts active joins.
    /// Plugin-owned continuations and Host compaction are not external-user
    /// arrivals. The registry itself remains attempt-scoped and drops the wake
    /// when no attempt is active.
    val observeChatMessage: registry: IJoinAttemptRegistry -> intent: ChatAdmissionIntent.Decision -> unit
