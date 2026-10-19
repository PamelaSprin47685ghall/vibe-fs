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
    let private views = Dictionary<string, AnchorViewDeclaration>()

    // DSL-MUTABLE: resource — sessionId → (anchor → summary standing in for it)
    let private summaries = Dictionary<string, Dictionary<int, string>>()

    let private sessionTable (store: Dictionary<string, Dictionary<int, string>>) (sessionId: string) =
        match store.TryGetValue sessionId with
        | true, existing -> existing
        | false, _ ->
            let fresh = Dictionary<int, string>()
            store.[sessionId] <- fresh
            fresh

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

    let anchors (sessionId: string) : (int * string) list =
        match numbers.TryGetValue sessionId with
        | false, _ -> []
        | true, session ->
            session
            |> Seq.map (fun pair -> pair.Value, pair.Key)
            |> Seq.sortBy fst
            |> Seq.toList

    let setView (sessionId: string) (declaration: AnchorViewDeclaration) : unit = views.[sessionId] <- declaration

    let takeView (sessionId: string) : AnchorViewDeclaration option =
        match views.TryGetValue sessionId with
        | true, declaration ->
            views.Remove sessionId |> ignore
            Some declaration
        | false, _ -> None

    /// A summary is permanent: from here on the anchor's segments are replaced
    /// by it, whether or not a later declaration names that anchor again.
    let replaceAnchors (sessionId: string) (pairs: (int * string) list) : unit =
        if not (List.isEmpty pairs) then
            let session = sessionTable summaries sessionId

            pairs |> List.iter (fun (anchor, summary) -> session.[anchor] <- summary)

    let replacements (sessionId: string) : Map<int, string> =
        match summaries.TryGetValue sessionId with
        | true, existing -> existing |> Seq.map (fun pair -> pair.Key, pair.Value) |> Map.ofSeq
        | false, _ -> Map.empty

    let clearSession (sessionId: string) : unit =
        numbers.Remove sessionId |> ignore
        views.Remove sessionId |> ignore
        summaries.Remove sessionId |> ignore

    let clearAll () : unit =
        numbers.Clear()
        views.Clear()
        summaries.Clear()
        nextNumber.Value <- 0
