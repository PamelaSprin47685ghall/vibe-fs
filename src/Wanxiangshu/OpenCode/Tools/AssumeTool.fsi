namespace Wanxiangshu.OpenCode

/// One process-local persistent JSON canvas interpreted by jq.
module AssumeTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/assume/description"

        [<Literal>]
        val ArgUpdate: string = "tool/assume/arg-update"

        [<Literal>]
        val ArgQuery: string = "tool/assume/arg-query"

    val admission: ToolAdmission

    val spec: factory: HostToolFactory -> ToolSpec
