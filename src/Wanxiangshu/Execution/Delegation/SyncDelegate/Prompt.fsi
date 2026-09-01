namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open Wanxiangshu.Foundation

type SyncDelegatePromptRequest =
    { Charge: string
      ProviderPrompt: LlmFacing.Document }

[<RequireQualifiedAccess>]
module SyncDelegatePrompt =
    val raw: charge: string -> SyncDelegatePromptRequest
    val withProviderPrompt: charge: string -> providerPrompt: LlmFacing.Document -> SyncDelegatePromptRequest
