namespace Wanxiangshu.Infrastructure

open System.Collections.Generic

/// Process-local staged Case for one Bookkeeper transaction.
/// The provider sees one atomic question/answer object through js-bookkeeper;
/// no filesystem-shaped Q.md/A.md surface exists here.
module BookkeeperStaging =

    type private Slot = { Question: string; Answer: string }

    let private gate = obj ()
    let private slots = Dictionary<string, Slot>()
    let private missingTransaction = "js-bookkeeper: no staged transaction"

    let beginTransaction (txId: string) (question: string) (answer: string) : unit =
        lock gate (fun () -> slots.[txId] <- { Question = question; Answer = answer })

    let snapshot (txId: string) : Result<string * string, string> =
        lock gate (fun () ->
            match slots.TryGetValue txId with
            | false, _ -> Error missingTransaction
            | true, slot -> Ok(slot.Question, slot.Answer))

    let apply
        (txId: string)
        (question: string option)
        (answer: string option)
        : Result<unit, string> =
        lock gate (fun () ->
            match slots.TryGetValue txId with
            | false, _ -> Error missingTransaction
            | true, slot ->
                slots.[txId] <-
                    { Question = Option.defaultValue slot.Question question
                      Answer = Option.defaultValue slot.Answer answer }

                Ok())

    let take (txId: string) : Result<string * string, string> =
        lock gate (fun () ->
            match slots.TryGetValue txId with
            | false, _ -> Error missingTransaction
            | true, slot ->
                slots.Remove txId |> ignore
                Ok(slot.Question, slot.Answer))

    let abort (txId: string) : unit =
        lock gate (fun () -> slots.Remove txId |> ignore)
