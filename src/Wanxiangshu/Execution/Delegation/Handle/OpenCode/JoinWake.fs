namespace Wanxiangshu.Execution.Delegation.Handle.OpenCode

open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Interaction.Dispatch.OpenCode

[<RequireQualifiedAccess>]
module JoinWake =

    /// EXEC-017: only a real external user material interrupts active joins.
    /// Plugin-owned continuations and Host compaction are not external-user
    /// arrivals. The registry itself remains attempt-scoped and drops the wake
    /// when no attempt is active.
    let observeChatMessage (registry: IJoinAttemptRegistry) (decoded: PromptIngressCodec.DecodedMessage) =
        match decoded.SessionId, decoded.PhysicalUserMessageId, decoded.PromptKey, decoded.IsHostCompaction with
        | Some sessionId, Some _, None, false -> registry.SignalUserMessage sessionId
        | _ -> ()
