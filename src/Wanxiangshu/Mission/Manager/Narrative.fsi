namespace Wanxiangshu.Mission.Manager

module ManagerNarrative =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val PlanningTable: string = "lifecycle/manager/planning-table"

        [<Literal>]
        val T1Revelation: string = "lifecycle/manager/t1-revelation"

        [<Literal>]
        val Reawakening: string = "lifecycle/manager/reawakening"

        [<Literal>]
        val IdlePreT1: string = "lifecycle/manager/idle-pre-t1"

        [<Literal>]
        val IdlePostT1: string = "lifecycle/manager/idle-post-t1"

    type NarrativePart = { Text: string; Synthetic: bool }
    type NarrativeProjection = { Parts: NarrativePart list }

    val firstBirth: userTextRaw: string -> planningTableDocument: string -> NarrativeProjection

    val reawakening:
        userTextRaw: string -> reawakeningDocument: string -> planningTableDocument: string -> NarrativeProjection

    val wrapT1AcceptedResult: t1RevelationInstructions: string list -> todoWriteResult: string -> string
    val renderText: projection: NarrativeProjection -> string
