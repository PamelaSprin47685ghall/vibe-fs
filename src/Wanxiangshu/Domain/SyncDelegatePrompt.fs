namespace Wanxiangshu.Domain

/// EXEC-032: semantic assignment and provider bytes are deliberately distinct.
type SyncDelegatePromptRequest =
    { Charge: string
      ProviderPrompt: string }

/// ARCH-010 renderers for SyncDelegate synthetic surfaces (EXEC-026 / EXEC-031).
[<RequireQualifiedAccess>]
module SyncDelegatePrompt =

    let raw (charge: string) =
        { Charge = charge
          ProviderPrompt = charge }

    let withProviderPrompt (charge: string) (providerPrompt: string) =
        { Charge = charge
          ProviderPrompt = providerPrompt }

    /// Idle nudge when a SyncDelegate turn fails without completing — no return tool.
    let idleNudge =
        SyntheticToml.document
            [ "The caller is still waiting. Continue the investigation and finish with an ordinary assistant completion when the charge is answered." ]
            []
