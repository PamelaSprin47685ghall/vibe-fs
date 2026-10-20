namespace Wanxiangshu.Repository.Programming.Js.Anchor

open System.Collections.Generic

/// Process-live numbering, pending declarations and ordered interval replacements.
module AnchorLedger =
    // DSL-MUTABLE: resource — process-wide stable anchor numbers
    let private nextNumber = ref 0
    // DSL-MUTABLE: resource — session message identities
    let private numbers = Dictionary<string, Dictionary<string, int>>()
    // DSL-MUTABLE: resource — next-request declarations
    let private views = Dictionary<string, AnchorViewDeclaration>()
    // DSL-MUTABLE: resource — permanent interval replacements in causal order
    let private histories = Dictionary<string, AnchorReplacement list>()
    // DSL-MUTABLE: resource — current caller context for view-based handoff
    let private contexts = Dictionary<string, (string option * string) list>()

    let remember sessionId messages = contexts.[sessionId] <- messages

    let context sessionId =
        match contexts.TryGetValue sessionId with
        | true, messages -> messages
        | false, _ -> []

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

    let private numbered (sessionId: string) =
        match numbers.TryGetValue sessionId with
        | false, _ -> []
        | true, session ->
            session
            |> Seq.map (fun pair -> pair.Value, pair.Key)
            |> Seq.sortBy fst
            |> Seq.toList

    let knownAnchors sessionId = numbered sessionId |> List.map fst

    let replacements (sessionId: string) : AnchorReplacement list =
        match histories.TryGetValue sessionId with
        | true, history -> history
        | false, _ -> []

    let anchors sessionId =
        let live =
            AnchorViewDeclaration.live (knownAnchors sessionId) (replacements sessionId)
            |> Set.ofList

        numbered sessionId |> List.filter (fun (anchor, _) -> Set.contains anchor live)

    let setView (sessionId: string) (declaration: AnchorViewDeclaration) : unit =
        let history = replacements sessionId

        let changes =
            AnchorViewDeclaration.plan (knownAnchors sessionId) history declaration

        histories.[sessionId] <- history @ changes
        views.[sessionId] <- declaration

    let takeView (sessionId: string) : AnchorViewDeclaration option =
        match views.TryGetValue sessionId with
        | true, declaration ->
            views.Remove sessionId |> ignore
            Some declaration
        | false, _ -> None

    let clearSession sessionId =
        numbers.Remove sessionId |> ignore
        views.Remove sessionId |> ignore
        histories.Remove sessionId |> ignore
        contexts.Remove sessionId |> ignore

    let clearAll () =
        numbers.Clear()
        views.Clear()
        histories.Clear()
        contexts.Clear()
        nextNumber.Value <- 0
