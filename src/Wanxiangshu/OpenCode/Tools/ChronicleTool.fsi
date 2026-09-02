namespace Wanxiangshu.OpenCode

open Wanxiangshu.Context.Companion.Blogger.Runtime

/// docs/what/enforcer.md — the `chronicle` tool (ENFORCER-010/020/040/041/061 tip v2).
/// Provider schema: required `entry` + required `tip`; no legacy blog/text alias.
module ChronicleTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/chronicle/description"

        [<Literal>]
        val Remembered: string = "tool/chronicle/remembered"

        [<Literal>]
        val NothingToRemember: string = "tool/chronicle/nothing-to-remember"

        [<Literal>]
        val MissingTip: string = "tool/chronicle/missing-tip"

    val EmptyTextError: string

    val NoLiveCycleError: string

    val tryCanonicalText: rawText: string -> Result<string, string>

    val hasLiveCycle: bloggerHost: IBloggerRuntimeHost option -> sessionId: string -> bool

    val tipFieldNames: unit -> string list

    val admission: bloggerHost: IBloggerRuntimeHost option -> ToolAdmission

    val spec:
        factory: HostToolFactory -> runtime: ToolRuntimeScope -> bloggerHost: IBloggerRuntimeHost option -> ToolSpec
