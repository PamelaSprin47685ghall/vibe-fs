namespace Wanxiangshu.Repository.Programming.Js.Anchor

open System.Collections.Generic

/// Process-live ANCHOR ledger. Module state by construction: restart resets
/// numbers and drops pending declarations, matching the process-live parked
/// sites.
module AnchorLedger =

    // DSL-MUTABLE: resource — process-live anchor stream
    let private nextNumber = ref 0

    // DSL-MUTABLE: resource — sessionId → (messageId → anchor)
    let private numbers = Dictionary<string, Dictionary<string, int>>()

    // DSL-MUTABLE: resource — sessionId → pending one-turn declaration
    let private views = Dictionary<string, AnchorViewDeclaration>()

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

    let clearSession (sessionId: string) : unit =
        numbers.Remove sessionId |> ignore
        views.Remove sessionId |> ignore

    let clearAll () : unit =
        numbers.Clear()
        views.Clear()
        nextNumber.Value <- 0
