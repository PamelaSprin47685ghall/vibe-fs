namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open Wanxiangshu.Foundation

/// EXEC-032: semantic assignment and provider bytes are deliberately distinct.
type SyncDelegatePromptRequest =
    { Charge: string
      ProviderPrompt: LlmFacing.Document }

/// ARCH-010 renderers for SyncDelegate synthetic surfaces (EXEC-026 / EXEC-031).
[<RequireQualifiedAccess>]
module SyncDelegatePrompt =

    let raw (charge: string) =
        { Charge = charge
          ProviderPrompt = LlmFacing.instruction charge }

    let withProviderPrompt (charge: string) (providerPrompt: LlmFacing.Document) =
        { Charge = charge
          ProviderPrompt = providerPrompt }
