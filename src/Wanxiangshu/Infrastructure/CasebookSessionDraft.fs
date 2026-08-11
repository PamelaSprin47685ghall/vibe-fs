namespace Wanxiangshu.Infrastructure

open System.Collections.Generic

type CasebookDraft =
    { Q: string
      mutable A: string option }

module CasebookDraftStore =
    let private drafts = Dictionary<string, CasebookDraft>()
    let private gate = obj ()
    let setQ (sessionId: string) (q: string) =
        lock gate (fun () -> drafts.[sessionId] <- { Q = q; A = None })
    let setA (sessionId: string) (a: string) =
        lock gate (fun () ->
            match drafts.TryGetValue sessionId with
            | true, d -> d.A <- Some a
            | false, _ -> drafts.[sessionId] <- { Q = ""; A = Some a })
    let tryTake (sessionId: string) : CasebookDraft option =
        lock gate (fun () ->
            match drafts.TryGetValue sessionId with
            | true, d -> drafts.Remove sessionId |> ignore; Some d
            | false, _ -> None)
    let clear (sessionId: string) = lock gate (fun () -> drafts.Remove sessionId |> ignore)
