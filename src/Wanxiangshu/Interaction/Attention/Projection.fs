// primary_owner: attention-regulation — AttentionRegulation.SurfaceSurface — KEEP — attention-regulation-surface verified
namespace Wanxiangshu.Interaction.Attention

open Wanxiangshu.Foundation.Identity

type DeferredWorkItem =
    { OccurrenceId: string
      Text: string
      ResurfacedBy: string option }

type AttentionProjectionState =
    { BySession: Map<SessionId, DeferredWorkItem list> }

[<RequireQualifiedAccess>]
module AttentionProjection =

    let empty = { BySession = Map.empty }

    let private items sessionId state =
        Map.tryFind sessionId state.BySession |> Option.defaultValue []

    let pending sessionId state =
        items sessionId state |> List.filter (fun item -> item.ResurfacedBy.IsNone)

    let tryFind sessionId occurrenceId state =
        items sessionId state
        |> List.tryFind (fun item -> item.OccurrenceId = occurrenceId)

    let record sessionId occurrenceId text state =
        let current = items sessionId state

        if current |> List.exists (fun item -> item.OccurrenceId = occurrenceId) then
            state
        else
            { state with
                BySession =
                    Map.add
                        sessionId
                        (current
                         @ [ { OccurrenceId = occurrenceId
                               Text = text
                               ResurfacedBy = None } ])
                        state.BySession }

    let resurface sessionId learningOccurrence workIds state =
        let selected = Set.ofList workIds

        let updated =
            items sessionId state
            |> List.map (fun item ->
                if Set.contains item.OccurrenceId selected && item.ResurfacedBy.IsNone then
                    { item with
                        ResurfacedBy = Some learningOccurrence }
                else
                    item)

        { state with
            BySession = Map.add sessionId updated state.BySession }
