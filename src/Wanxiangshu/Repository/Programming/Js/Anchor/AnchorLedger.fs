namespace Wanxiangshu.Repository.Programming.Js.Anchor

open System.Collections.Generic

/// Process-live ANCHOR ledger. Module state by construction: restart resets
/// numbers, drops pending declarations and forgets which anchors were
/// summarized, matching the process-live parked sites.
module AnchorLedger =

    // DSL-MUTABLE: resource — process-live anchor stream
    let private nextNumber = ref 0

    // DSL-MUTABLE: resource — sessionId → (messageId → anchor)
    let private numbers = Dictionary<string, Dictionary<string, int>>()

    // DSL-MUTABLE: resource — sessionId → pending one-turn declaration
    let private views = Dictionary<string, AnchorViewDeclaration * int list>()

    // DSL-MUTABLE: resource — session-owned ordered replacement history
    let private histories = Dictionary<string, AnchorReplacement list>()

    // DSL-MUTABLE: resource — session-owned source material
    let private contexts = Dictionary<string, (string option * string) list>()

    // DSL-MUTABLE: resource — authoritative Plan user anchors
    let private protectedAnchors = Dictionary<string, Set<int>>()

    let remember sessionId messages = contexts.[sessionId] <- messages

    let context sessionId =
        match contexts.TryGetValue sessionId with
        | true, messages -> messages
        | false, _ -> []

    let replacements sessionId =
        match histories.TryGetValue sessionId with
        | true, history -> history
        | false, _ -> []

    let private overlaps anchors change =
        anchors
        |> Set.exists (fun anchor -> anchor >= change.Anchor && anchor < change.Until)

    let protect sessionId anchors =
        protectedAnchors.[sessionId] <- anchors
        histories.[sessionId] <- replacements sessionId |> List.filter (overlaps anchors >> not)

    let numberMessage (sessionId: string) (messageId: string) : int =
        let session =
            match numbers.TryGetValue sessionId with
            | true, existing -> existing
            | false, _ ->
                let fresh = Dictionary<string, int>()
                numbers.[sessionId] <- fresh
                fresh

        match session.TryGetValue messageId with
        | true, anchor -> anchor
        | false, _ ->
            nextNumber.Value <- nextNumber.Value + 1
            session.[messageId] <- nextNumber.Value
            nextNumber.Value

    let private numbered (sessionId: string) : (int * string) list =
        match numbers.TryGetValue sessionId with
        | false, _ -> []
        | true, session ->
            session
            |> Seq.map (fun pair -> pair.Value, pair.Key)
            |> Seq.sortBy fst
            |> Seq.toList

    let knownAnchors sessionId = numbered sessionId |> List.map fst

    let anchors sessionId =
        let surviving =
            AnchorViewDeclaration.live (knownAnchors sessionId) (replacements sessionId)
            |> Set.ofList

        numbered sessionId
        |> List.filter (fun (anchor, _) -> Set.contains anchor surviving)

    let setView sessionId declaration =
        let history = replacements sessionId

        let changes =
            AnchorViewDeclaration.plan (knownAnchors sessionId) history declaration

        let protectedSet =
            match protectedAnchors.TryGetValue sessionId with
            | true, anchors -> anchors
            | false, _ -> Set.empty

        let blocked, accepted = changes |> List.partition (overlaps protectedSet)
        histories.[sessionId] <- history @ accepted
        views.[sessionId] <- declaration, (blocked |> List.map _.Anchor)

    let protectedReplacements sessionId =
        match views.TryGetValue sessionId with
        | true, (_, blocked) -> blocked
        | false, _ -> []

    let takeView (sessionId: string) : AnchorViewDeclaration option =
        match views.TryGetValue sessionId with
        | true, (declaration, _) ->
            views.Remove sessionId |> ignore
            Some declaration
        | false, _ -> None

    let clearSession (sessionId: string) : unit =
        numbers.Remove sessionId |> ignore
        views.Remove sessionId |> ignore
        histories.Remove sessionId |> ignore
        contexts.Remove sessionId |> ignore
        protectedAnchors.Remove sessionId |> ignore

    let clearAll () : unit =
        numbers.Clear()
        views.Clear()
        histories.Clear()
        contexts.Clear()
        protectedAnchors.Clear()
        nextNumber.Value <- 0
