namespace Wanxiangshu.Mission.Manager

open System
open Wanxiangshu.Foundation

/// GLORY-014/064/074 + SURFACE-004: BlindPlan lifecycle text owner.
/// Production Birth / Reawakening use Planning Table (§7.4.1).
/// Prose meaning lives in `resources/provider/lifecycle/manager/**`; this module
/// owns semantic paths + canonical LlmFacing assembly (PROMPT-019).
module ManagerNarrative =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let PlanningTable = "lifecycle/manager/planning-table"

        [<Literal>]
        let T1Revelation = "lifecycle/manager/t1-revelation"

        [<Literal>]
        let Reawakening = "lifecycle/manager/reawakening"

        [<Literal>]
        let IdlePreT1 = "lifecycle/manager/idle-pre-t1"

        [<Literal>]
        let IdlePostT1 = "lifecycle/manager/idle-post-t1"

    type NarrativePart = { Text: string; Synthetic: bool }

    type NarrativeProjection = { Parts: NarrativePart list }

    let private humanPart (text: string) : NarrativePart = { Text = text; Synthetic = false }

    let private syntheticPart (text: string) : NarrativePart = { Text = text; Synthetic = true }

    /// GLORY-074: first-Life BlindPlan Opening — [human raw] + Planning Table document.
    let firstBirth (userTextRaw: string) (planningTableDocument: string) : NarrativeProjection =
        { Parts = [ humanPart userTextRaw; syntheticPart planningTableDocument ] }

    /// GLORY-074: Reawakening — prefix document + human raw + Planning Table.
    let reawakening
        (userTextRaw: string)
        (reawakeningDocument: string)
        (planningTableDocument: string)
        : NarrativeProjection =
        { Parts =
            [ syntheticPart reawakeningDocument
              humanPart userTextRaw
              syntheticPart planningTableDocument ] }

    /// TODO-015 / GLORY-074: canonical T1 tool result = entrustment revelation + enriched todo body.
    let wrapT1AcceptedResult (t1RevelationInstructions: string list) (todoWriteResult: string) =
        let normalized = LlmFacing.normalizeNewlines todoWriteResult

        if String.IsNullOrWhiteSpace normalized then
            LlmFacing.renderInstructions t1RevelationInstructions
        else
            LlmFacing.renderInstructions (t1RevelationInstructions @ [ normalized ])

    let renderText (projection: NarrativeProjection) =
        projection.Parts
        |> List.map (fun part -> part.Text.TrimEnd('\n'))
        |> String.concat "\n\n"
        |> fun s -> if s.EndsWith("\n") then s else s + "\n"
