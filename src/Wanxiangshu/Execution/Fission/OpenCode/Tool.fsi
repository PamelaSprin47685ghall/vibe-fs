namespace Wanxiangshu.Execution.Fission.OpenCode

open Wanxiangshu.OpenCode

/// Same-participant physical replacement. Fission never calls the Host session
/// fork endpoint: it creates fresh sibling sessions, starts them from the
/// canonical owner LWR + exact lane input, then physically interrupts the old
/// present without terminating the logical owner.
module FissionTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/fission/description"

        [<Literal>]
        val TooFew: string = "tool/fission/too-few"

        [<Literal>]
        val InvalidOrigin: string = "tool/fission/invalid-origin"

        [<Literal>]
        val Capacity: string = "tool/fission/capacity"

        [<Literal>]
        val AlreadyActive: string = "tool/fission/already-active"

        [<Literal>]
        val Unavailable: string = "tool/fission/unavailable"

        [<Literal>]
        val Started: string = "tool/fission/started"

        [<Literal>]
        val SharedCompletion: string = "tool/fission/shared-completion"

    val admission: ToolAdmission
    val spec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec
