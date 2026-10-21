namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Collections.Generic

/// Process-local staged Case for one Bookkeeper transaction.
/// The provider sees one atomic question/answer object through todowrite-for-bookkeeper;
/// no filesystem-shaped Q.md/A.md surface exists here.
module BookkeeperStaging =

    type private Slot =
        { mutable Question: string
          mutable Answer: string
          // LC-029: the initial Q/A must never pass as this run's delivery, so
          // the slot records whether the provider's program wrote through it.
          mutable Touched: bool }

    let private gate = obj ()
    // DSL-MUTABLE: resource — staged transaction slot registry
    let private slots = Dictionary<string, Slot>()
    let private missingTransaction = "todowrite-for-bookkeeper: no staged transaction"

    let beginTransaction (txId: string) (question: string) (answer: string) : unit =
        lock gate (fun () ->
            slots.[txId] <-
                { Question = question
                  Answer = answer
                  Touched = false })

    let snapshot (txId: string) : Result<string * string, string> =
        lock gate (fun () ->
            match slots.TryGetValue txId with
            | false, _ -> Error missingTransaction
            | true, slot -> Ok(slot.Question, slot.Answer))

    let apply (txId: string) (question: string option) (answer: string option) : Result<unit, string> =
        lock gate (fun () ->
            match slots.TryGetValue txId with
            | false, _ -> Error missingTransaction
            | true, slot ->
                slot.Question <- Option.defaultValue slot.Question question
                slot.Answer <- Option.defaultValue slot.Answer answer
                slot.Touched <- true

                Ok())

    let take (txId: string) : Result<string * string, string> =
        lock gate (fun () ->
            match slots.TryGetValue txId with
            | false, _ -> Error missingTransaction
            | true, slot when not slot.Touched -> Error "the staged transaction was not written by this completion"
            | true, slot ->
                slots.Remove txId |> ignore
                Ok(slot.Question, slot.Answer))

    /// LC-029: whether the provider's program actually wrote through the
    /// staged transaction. A terminal with no staged write has not delivered.
    let touched (txId: string) : bool =
        lock gate (fun () ->
            match slots.TryGetValue txId with
            | true, slot -> slot.Touched
            | false, _ -> false)

    let abort (txId: string) : unit =
        lock gate (fun () -> slots.Remove txId |> ignore)
