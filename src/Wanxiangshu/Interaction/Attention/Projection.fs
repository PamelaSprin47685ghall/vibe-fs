namespace Wanxiangshu.Interaction.Attention

open Wanxiangshu.Foundation.Identity

type DeferredWorkItem =
    { OccurrenceId: string
      Text: string }

type AttentionProjectionState =
    { BySession: Map<SessionId, DeferredWorkItem list> }

[<RequireQualifiedAccess>]
module AttentionProjection =

    let empty = { BySession = Map.empty }

    let private items sessionId state =
        Map.tryFind sessionId state.BySession |> Option.defaultValue []

    let pending sessionId state = items sessionId state

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
                               Text = text } ])
                        state.BySession }

    /// Consume (and thereby extinguish) the named DeferredWork occurrences. A
    /// consumed occurrence leaves the projection; unknown ids are ignored, so
    /// replay is idempotent.
    let consume sessionId workIds state =
        let selected = Set.ofList workIds

        let remaining =
            items sessionId state
            |> List.filter (fun item -> not (Set.contains item.OccurrenceId selected))

        { state with
            BySession = Map.add sessionId remaining state.BySession }

    /// ATTENTION-004: a life that ends before consumption takes its remaining
    /// entries with it — a reused SessionId starts a fresh life and must not
    /// inherit the closed life's pending work.
    let closeLife sessionId state =
        { state with
            BySession = Map.remove sessionId state.BySession }
