namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Collections.Generic

type CasebookTurn = { Q: string; A: string option }

type CasebookDraft = { Turns: CasebookTurn list }

module CasebookDraftStore =
    // DSL-MUTABLE: resource — per-session casebook draft registry
    let private drafts = Dictionary<string, CasebookDraft>()
    let private gate = obj ()

    let private updateQuestion draft q =
        match List.rev draft.Turns with
        | last :: earlier when last.A.IsNone -> { Turns = List.rev ({ last with Q = q } :: earlier) }
        | _ -> { Turns = draft.Turns @ [ { Q = q; A = None } ] }

    let private updateAnswer draft a =
        match List.rev draft.Turns with
        | last :: earlier -> { Turns = List.rev ({ last with A = Some a } :: earlier) }
        | [] -> { Turns = [ { Q = ""; A = Some a } ] }

    let setQ (sessionId: string) (q: string) =
        lock gate (fun () ->
            match drafts.TryGetValue sessionId with
            | true, draft -> drafts.[sessionId] <- updateQuestion draft q
            | false, _ -> drafts.[sessionId] <- { Turns = [ { Q = q; A = None } ] })

    let setA (sessionId: string) (a: string) =
        lock gate (fun () ->
            match drafts.TryGetValue sessionId with
            | true, draft -> drafts.[sessionId] <- updateAnswer draft a
            | false, _ -> drafts.[sessionId] <- { Turns = [ { Q = ""; A = Some a } ] })

    let tryTake (sessionId: string) : CasebookDraft option =
        lock gate (fun () ->
            match drafts.TryGetValue sessionId with
            | true, d ->
                drafts.Remove sessionId |> ignore
                Some d
            | false, _ -> None)

    let clear (sessionId: string) =
        lock gate (fun () -> drafts.Remove sessionId |> ignore)

    let transcript (turns: CasebookTurn list) : string =
        turns
        |> List.mapi (fun index turn ->
            let n = string (index + 1)
            let answer = defaultArg turn.A ""
            "Q" + n + " = " + turn.Q + "\nA" + n + " = " + answer)
        |> String.concat "\n"
