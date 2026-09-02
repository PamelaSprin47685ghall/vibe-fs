namespace Wanxiangshu.Context.Companion.Blogger

/// Blogger crash-window owner boundary. The pure classifier returns a stable
/// case name; request IDs and provider identities never cross into JS tests.
[<RequireQualifiedAccess>]
module BloggerCrashSurface =
    val classifyOpenRequest: hasPhysicalAccepted: bool -> hasCompletedBlogTool: bool -> hasCycleReceipt: bool -> string
