namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Delegation.SyncDelegate

/// DevOps synchronous Coder delegation via reusable SyncDelegate Session.
/// `establish-behavior` / `repair-behavior` replace the old coder(tdd=...) verb.
/// Ordinary assistant completion → bounded WorkRecord (EXEC-031).
module CoderTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<RequireQualifiedAccess>]
        module Establish =
            [<Literal>]
            val Description: string = "tool/establish-behavior/description"

            [<Literal>]
            val ArgCharge: string = "tool/establish-behavior/arg-charge"

            [<Literal>]
            val ArgKeywords: string = "tool/establish-behavior/arg-keywords"

            [<Literal>]
            val Unavailable: string = "tool/establish-behavior/unavailable"

            [<Literal>]
            val AuthorityRequired: string = "tool/establish-behavior/authority-required"

            [<Literal>]
            val NeedsCharge: string = "tool/establish-behavior/needs-charge"

            [<Literal>]
            val Incomplete: string = "tool/establish-behavior/incomplete"

        [<RequireQualifiedAccess>]
        module Repair =
            [<Literal>]
            val Description: string = "tool/repair-behavior/description"

            [<Literal>]
            val ArgCharge: string = "tool/repair-behavior/arg-charge"

            [<Literal>]
            val ArgKeywords: string = "tool/repair-behavior/arg-keywords"

            [<Literal>]
            val Unavailable: string = "tool/repair-behavior/unavailable"

            [<Literal>]
            val AuthorityRequired: string = "tool/repair-behavior/authority-required"

            [<Literal>]
            val NeedsCharge: string = "tool/repair-behavior/needs-charge"

            [<Literal>]
            val Incomplete: string = "tool/repair-behavior/incomplete"

    val behaviorAdmission: ToolAdmission

    val establishSpec:
        factory: HostToolFactory -> scope: ToolRuntimeScope -> syncDelegate: SyncDelegateRuntime option -> ToolSpec

    val repairSpec:
        factory: HostToolFactory -> scope: ToolRuntimeScope -> syncDelegate: SyncDelegateRuntime option -> ToolSpec
