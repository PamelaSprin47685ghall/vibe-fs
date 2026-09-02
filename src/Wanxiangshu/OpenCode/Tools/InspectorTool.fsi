namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Delegation.SyncDelegate

/// Synchronous Inspector delegation via reusable SyncDelegate Session.
/// Ordinary assistant completion → bounded WorkRecord (EXEC-031).
module InspectorTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/inspect/description"

        [<Literal>]
        val ArgCharge: string = "tool/inspect/arg-charge"

        [<Literal>]
        val ArgKeywords: string = "tool/inspect/arg-keywords"

        [<Literal>]
        val Unavailable: string = "tool/inspect/unavailable"

        [<Literal>]
        val AuthorityRequired: string = "tool/inspect/authority-required"

        [<Literal>]
        val NeedsCharge: string = "tool/inspect/needs-charge"

        [<Literal>]
        val Incomplete: string = "tool/inspect/incomplete"

    val admission: ToolAdmission

    val spec:
        factory: HostToolFactory -> scope: ToolRuntimeScope -> syncDelegate: SyncDelegateRuntime option -> ToolSpec
