namespace Wanxiangshu.Context.Companion.Blogger

/// Blogger crash-window owner boundary. The pure classifier returns a stable
/// case name; request IDs and provider identities never cross into JS tests.
[<RequireQualifiedAccess>]
module BloggerCrashSurface =
    let classifyOpenRequest (hasPhysicalAccepted: bool) (hasCompletedBlogTool: bool) (hasCycleReceipt: bool) : string =
        match BloggerCrashRecovery.classifyOpenRequest hasPhysicalAccepted hasCompletedBlogTool hasCycleReceipt with
        | None -> "ReceiptedIdle"
        | Some(BloggerCrashRecovery.WindowOutcome.AbandonedUnsent _) -> "AbandonedUnsent"
        | Some(BloggerCrashRecovery.WindowOutcome.Recommitted _) -> "Recommitted"
        | Some(BloggerCrashRecovery.WindowOutcome.RestoredInFlight _) -> "RestoredInFlight"
        | Some(BloggerCrashRecovery.WindowOutcome.ReceiptedIdle _) -> "ReceiptedIdle"
        | Some(BloggerCrashRecovery.WindowOutcome.PendingMaterial _) -> "PendingMaterial"
        | Some(BloggerCrashRecovery.WindowOutcome.AlreadyLive _) -> "AlreadyLive"
        | Some(BloggerCrashRecovery.WindowOutcome.Superseded _) -> "Superseded"
        | Some(BloggerCrashRecovery.WindowOutcome.Unreadable _) -> "Unreadable"
