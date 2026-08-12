namespace Wanxiangshu.Domain

/// ARCH-010 renderers for SyncDelegate synthetic surfaces (EXEC-026 / EXEC-031).
[<RequireQualifiedAccess>]
module SyncDelegatePrompt =

    /// Idle nudge when a SyncDelegate turn fails without completing — no return tool.
    let idleNudge =
        SyntheticToml.document
            [ "The caller is still waiting. Continue the investigation and finish with an ordinary assistant completion when the charge is answered." ]
            []
