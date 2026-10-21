namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

module AssumeTool =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/assume/description"

        [<Literal>]
        val Committed: string = "tool/assume/committed"

    val admission: ToolAdmission
    val spec: factory: HostToolFactory -> ToolSpec
