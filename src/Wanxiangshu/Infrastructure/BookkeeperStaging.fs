namespace Wanxiangshu.Infrastructure

open System
open System.Collections.Generic

/// Process-local staged Q/A bytes for one Bookkeeper transaction.
/// edit-qa is the only mutation path; there is no filesystem path.
module BookkeeperStaging =

    type private Slot =
        { mutable Q: string
          mutable A: string }

    let private gate = obj ()
    let private slots = Dictionary<string, Slot>()

    let private parseDocument (document: string) : Result<bool, string> =
        match document with
        | "Q.md" -> Ok true
        | "A.md" -> Ok false
        | _ -> Error "edit-qa: document must be Q.md or A.md"

    let private uniqueReplace (source: string) (oldText: string) (newText: string) : Result<string, string> =
        if String.IsNullOrEmpty oldText then
            Error "edit-qa: old_text must be non-empty"
        else
            let first = source.IndexOf oldText

            if first < 0 then
                Error "edit-qa: old_text not found"
            else
                let second = source.IndexOf(oldText, first + oldText.Length)

                if second >= 0 then
                    Error "edit-qa: old_text is ambiguous"
                else
                    Ok(source.Substring(0, first) + newText + source.Substring(first + oldText.Length))

    let beginTransaction (txId: string) (q: string) (a: string) : unit =
        lock gate (fun () -> slots.[txId] <- { Q = q; A = a })

    let read (txId: string) (document: string) : Result<string, string> =
        lock gate (fun () ->
            match parseDocument document, slots.TryGetValue txId with
            | Error err, _ -> Error err
            | Ok _, (false, _) -> Error "edit-qa: no staged transaction"
            | Ok true, (true, slot) -> Ok slot.Q
            | Ok false, (true, slot) -> Ok slot.A)

    let replace (txId: string) (document: string) (oldText: string) (newText: string) : Result<unit, string> =
        lock gate (fun () ->
            match parseDocument document, slots.TryGetValue txId with
            | Error err, _ -> Error err
            | Ok _, (false, _) -> Error "edit-qa: no staged transaction"
            | Ok isQ, (true, slot) ->
                let source = if isQ then slot.Q else slot.A

                match uniqueReplace source oldText newText with
                | Error err -> Error err
                | Ok next ->
                    if isQ then slot.Q <- next else slot.A <- next
                    Ok())

    let take (txId: string) : Result<string * string, string> =
        lock gate (fun () ->
            match slots.TryGetValue txId with
            | false, _ -> Error "edit-qa: no staged transaction"
            | true, slot ->
                slots.Remove txId |> ignore
                Ok(slot.Q, slot.A))

    let abort (txId: string) : unit =
        lock gate (fun () -> slots.Remove txId |> ignore)
