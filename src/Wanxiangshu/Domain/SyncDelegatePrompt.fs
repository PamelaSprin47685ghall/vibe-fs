namespace Wanxiangshu.Domain

/// EXEC-032: semantic assignment and provider bytes are deliberately distinct.
type SyncDelegatePromptRequest =
    { Charge: string
      ProviderPrompt: string }

/// ARCH-010 renderers for SyncDelegate synthetic surfaces (EXEC-026 / EXEC-031).
[<RequireQualifiedAccess>]
module SyncDelegatePrompt =

    /// Idle nudge when a SyncDelegate turn fails without completing — no return tool.
    let IdleNudge = "delegation/sync-idle"

    let raw (charge: string) =
        { Charge = charge
          ProviderPrompt = charge }

    let withProviderPrompt (charge: string) (providerPrompt: string) =
        { Charge = charge
          ProviderPrompt = providerPrompt }

    let idleNudgeDocument (instructionLines: string list) =
        SyntheticToml.document instructionLines []
