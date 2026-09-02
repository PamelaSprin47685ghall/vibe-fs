namespace Wanxiangshu.Execution.Session.OpenCode

open Wanxiangshu.OpenCode

/// horizon() — natural-language roster of who remains at the caller's horizon.
module HorizonTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/horizon/description"

        [<Literal>]
        val Returned: string = "tool/horizon/returned"

        [<Literal>]
        val StillAway: string = "tool/horizon/still-away"

        [<Literal>]
        val DidNotReturn: string = "tool/horizon/did-not-return"

        [<Literal>]
        val RemainsOpen: string = "tool/horizon/remains-open"

        [<Literal>]
        val Someone: string = "tool/horizon/someone"

        [<Literal>]
        val TerminalLabel: string = "tool/horizon/terminal-label"

        [<Literal>]
        val EmptyRoster: string = "tool/horizon/empty-roster"

        [<Literal>]
        val UnavailableFromContext: string = "tool/horizon/unavailable-from-context"

        [<Literal>]
        val CannotBeSeen: string = "tool/horizon/cannot-be-seen"

        [<Literal>]
        val LatestWork: string = "tool/horizon/latest-work"

        [<Literal>]
        val NoWorkYet: string = "tool/horizon/no-work-yet"

        [<Literal>]
        val LatestWorkUnavailable: string = "tool/horizon/latest-work-unavailable"

    val admission: ToolAdmission
    val spec: scope: ToolRuntimeScope -> ToolSpec
