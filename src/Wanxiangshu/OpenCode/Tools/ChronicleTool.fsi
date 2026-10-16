namespace Wanxiangshu.OpenCode

open Wanxiangshu.Context.Companion.Blogger.Runtime

/// docs/what/enforcer.md — the `chronicle` tool (ENFORCER-010/020/040/041/061 tip v2).
/// Provider schema: required charge/occurrence/settlement/consequence, a free tipName
/// and seven lesson sections, plus optional evidence.
module ChronicleTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/chronicle/description"

        [<Literal>]
        val ArgCharge: string = "tool/chronicle/arg-charge"

        [<Literal>]
        val ArgOccurrence: string = "tool/chronicle/arg-occurrence"

        [<Literal>]
        val ArgSettlement: string = "tool/chronicle/arg-settlement"

        [<Literal>]
        val ArgConsequence: string = "tool/chronicle/arg-consequence"

        [<Literal>]
        val ArgEvidence: string = "tool/chronicle/arg-evidence"

        [<Literal>]
        val ArgTipName: string = "tool/chronicle/arg-tip-name"

        [<Literal>]
        val ArgWhatToDoNow: string = "tool/chronicle/arg-what-to-do-now"

        [<Literal>]
        val ArgWhyThisMatters: string = "tool/chronicle/arg-why-this-matters"

        [<Literal>]
        val ArgRepairStrategy: string = "tool/chronicle/arg-repair-strategy"

        [<Literal>]
        val ArgDecisionBranches: string = "tool/chronicle/arg-decision-branches"

        [<Literal>]
        val ArgCommonWrongFixes: string = "tool/chronicle/arg-common-wrong-fixes"

        [<Literal>]
        val ArgVerification: string = "tool/chronicle/arg-verification"

        [<Literal>]
        val ArgDoneWhen: string = "tool/chronicle/arg-done-when"

        [<Literal>]
        val Remembered: string = "tool/chronicle/remembered"

        [<Literal>]
        val NothingToRemember: string = "tool/chronicle/nothing-to-remember"

        [<Literal>]
        val InvalidTip: string = "tool/chronicle/invalid-tip"

    [<Literal>]
    val EmptyTextError: string = "CHRONICLE_EMPTY_ENFORCER_061"

    [<Literal>]
    val NoLiveCycleError: string = "CHRONICLE_NO_LIVE_CYCLE"

    val tryCanonicalText: rawText: string -> Result<string, string>

    val hasLiveCycle: bloggerHost: IBloggerRuntimeHost option -> sessionId: string -> bool

    val tipFieldNames: unit -> string list

    val admission: bloggerHost: IBloggerRuntimeHost option -> ToolAdmission

    val spec:
        factory: HostToolFactory ->
        terminateSession: (string * string -> System.Threading.Tasks.Task<Result<unit, string>>) ->
        bloggerHost: IBloggerRuntimeHost option ->
            ToolSpec
