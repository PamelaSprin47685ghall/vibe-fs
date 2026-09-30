namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

module AssumeTool =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/assume/description"

        [<Literal>]
        val ArgAssumption: string = "tool/assume/arg-assumption"

        [<Literal>]
        val Committed: string = "tool/assume/committed"

    val roleAllowed: role: Role -> bool
    val admission: ToolAdmission
    val spec: factory: HostToolFactory -> ToolSpec
