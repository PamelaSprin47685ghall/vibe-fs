namespace Wanxiangshu.Interaction.Attention

open Wanxiangshu.Foundation.Identity

type DeferredWorkItem =
    { OccurrenceId: string
      Text: string }

type AttentionProjectionState =
    { BySession: Map<SessionId, DeferredWorkItem list>
      ConsumedBySession: Map<SessionId, Set<string>> }

[<RequireQualifiedAccess>]
module AttentionProjection =

    let empty =
        { BySession = Map.empty
          ConsumedBySession = Map.empty }

    let private items sessionId state =
        Map.tryFind sessionId state.BySession |> Option.defaultValue []

    let pending sessionId state = items sessionId state

    let tryFind sessionId occurrenceId state =
        items sessionId state
        |> List.tryFind (fun item -> item.OccurrenceId = occurrenceId)

    /// ATTENTION-004/006: a consumed occurrence leaves a consumption receipt
    /// in the projection, so a replayed record cannot resurrect it.
    let wasConsumed sessionId occurrenceId state =
        match Map.tryFind sessionId state.ConsumedBySession with
        | Some consumed -> Set.contains occurrenceId consumed
        | None -> false

    let record sessionId occurrenceId text state =
        let current = items sessionId state

        let alreadyKnown =
            current |> List.exists (fun item -> item.OccurrenceId = occurrenceId)
            || wasConsumed sessionId occurrenceId state

        if alreadyKnown then
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
    /// consumed occurrence leaves the projection and records a consumption
    /// receipt, so a replayed record cannot resurrect it; unknown ids are
    /// ignored, so replay is idempotent.
    let consume sessionId workIds state =
        let selected = Set.ofList workIds
        let current = items sessionId state

        let consumedNow =
            current
            |> List.filter (fun item -> Set.contains item.OccurrenceId selected)
            |> List.map (fun item -> item.OccurrenceId)
            |> Set.ofList

        let remaining =
            items sessionId state
            |> List.filter (fun item -> not (Set.contains item.OccurrenceId selected))

        let consumed =
            Map.tryFind sessionId state.ConsumedBySession
            |> Option.defaultValue Set.empty
            |> Set.union consumedNow

        { state with
            BySession = Map.add sessionId remaining state.BySession
            ConsumedBySession = Map.add sessionId consumed state.ConsumedBySession }

    /// ATTENTION-004: a life that ends before consumption takes its remaining
    /// entries with it — a reused SessionId starts a fresh life and must not
    /// inherit the closed life's pending work.
    let closeLife sessionId state =
        { state with
            BySession = Map.remove sessionId state.BySession }
