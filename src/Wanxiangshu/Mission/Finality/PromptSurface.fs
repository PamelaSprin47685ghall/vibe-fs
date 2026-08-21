namespace Wanxiangshu.Mission.Finality

open Wanxiangshu.Foundation
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

/// JSON-native owner surface for Manager and Finality provider prose.
///
/// Narrative and rendering semantics remain in ManagerNarrative / FinalityPrompt;
/// resource loading remains ProviderResources / ProviderProse. This module only
/// closes that production boundary for semantic tests without exposing Fable
/// records, lists, or unions.
[<RequireQualifiedAccess>]
module PromptSurface =

    let private english = ProviderLanguage.English

    let private rawResource (semanticPath: string) : string =
        ProviderResources.readText english semanticPath
        |> LlmFacing.normalizeNewlines
        |> fun text -> text.Trim()

    let private document (semanticPath: string) : string =
        ProviderProse.document english semanticPath Map.empty
        |> LlmFacing.normalizeNewlines

    let private instructionLines (semanticPath: string) : string list =
        ProviderProse.instructionLines english semanticPath Map.empty

    let private narrativePartView (part: ManagerNarrative.NarrativePart) : obj =
        box
            {| text = part.Text
               synthetic = part.Synthetic |}

    let private narrativeView (projection: ManagerNarrative.NarrativeProjection) : obj =
        box
            {| parts = projection.Parts |> List.map narrativePartView |> List.toArray
               text = ManagerNarrative.renderText projection |}

    let reawakeningPrefix () : string =
        let lines = (rawResource ManagerNarrative.Path.Reawakening).Split '\n'
        if lines.Length = 0 then "" else lines.[0]

    let planningTableDocument () : string =
        document ManagerNarrative.Path.PlanningTable

    let t1RevelationDocument () : string =
        document ManagerNarrative.Path.T1Revelation

    let wrapT1AcceptedResult (todoWriteResult: string) : string =
        ManagerNarrative.wrapT1AcceptedResult (instructionLines ManagerNarrative.Path.T1Revelation) todoWriteResult

    let firstBirth (userTextRaw: string) : obj =
        ManagerNarrative.firstBirth userTextRaw (planningTableDocument ())
        |> narrativeView

    let reawakening (userTextRaw: string) : obj =
        ManagerNarrative.reawakening userTextRaw (document ManagerNarrative.Path.Reawakening) (planningTableDocument ())
        |> narrativeView

    let firstBirthText (userTextRaw: string) : string =
        ManagerNarrative.firstBirth userTextRaw (planningTableDocument ())
        |> ManagerNarrative.renderText

    let reawakeningText (userTextRaw: string) : string =
        ManagerNarrative.reawakening userTextRaw (document ManagerNarrative.Path.Reawakening) (planningTableDocument ())
        |> ManagerNarrative.renderText

    let workActivation () : string =
        document ManagerLifecyclePrompt.Path.WorkActivation

    let idleEncouragementPreT1 () : string =
        document ManagerLifecyclePrompt.Path.IdleEncouragementPreT1

    let idleEncouragementPostT1 () : string =
        document ManagerLifecyclePrompt.Path.IdleEncouragementPostT1

    let rejected (reviewerWorkRecord: string) : string =
        FinalityPrompt.rejected (instructionLines FinalityPrompt.Path.Rejected) reviewerWorkRecord

    let blessed (workRecordBundle: string) : string =
        FinalityPrompt.blessed (instructionLines FinalityPrompt.Path.Blessed) workRecordBundle

    let rest () : string = document FinalityPrompt.Path.Rest

    let managerSystemPrompt () : string = rawResource "role/manager"

    let reviewerSystemPrompt () : string = rawResource "role/reviewer"
