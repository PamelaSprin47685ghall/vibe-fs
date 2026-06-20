module VibeFs.Kernel.LoopMessages

/// The canonical markers that signal review-mode transitions inside the dialogue
/// history.  These are the single source of truth shared by every producer
/// (slash-command activation, submit_review rendering) and by the reconstruction
/// logic in `ReviewSession.inferReviewTaskFromTexts` that replays them after an
/// opencode restart — so the wording written and the wording parsed can never
/// drift apart.
let taskActivatePrefix = "Task (With Review): "
let cancelledMarker = "With-Review Mode cancelled."
let acceptedEndMarker = "With-Review Mode has ended."

let loopFooter =
    [ "- report: a detailed description of what you did and why"
      "- affectedFiles: list of every file you modified or created"
      ""
      "A reviewer will examine your submission. If accepted, you are done. If rejected, you will receive specific feedback to address." ]

let buildLoopMessage (task: string) (bodyLines: string list) : string =
    [ taskActivatePrefix + task; "" ] @ bodyLines @ loopFooter |> String.concat "\n"

// ── Reconstruction from dialogue history ─────────────────────────────────────

/// After an opencode restart the in-memory review store is gone, but the
/// dialogue history still carries the markers this module itself writes.  These
/// pure folds replay that history to recover the current review task — the
/// history is the single source of truth and the store is merely a re-buildable
/// projection of it.  Living next to the markers guarantees writer/reader can
/// never drift apart.
let private tryActivateTask (text: string) : string option =
    text.Split('\n')
    |> Seq.tryPick (fun line ->
        let trimmed = line.Trim()
        if trimmed.StartsWith(taskActivatePrefix) then
            let task = trimmed.Substring(taskActivatePrefix.Length).Trim()
            if task = "" then None else Some task
        else None)

let private isReviewEndMarker (text: string) : bool =
    text.Contains(cancelledMarker) || text.Contains(acceptedEndMarker)

/// Fold over chronological text fragments: an activation line records the task,
/// an end marker (cancel or accept) clears it.  Reject and terminated rounds
/// say "still active" and therefore deliberately do not match the end markers.
let inferReviewTaskFromTexts (texts: string seq) : string option =
    texts
    |> Seq.fold (fun current text ->
        if isNull text || text = "" then current
        elif isReviewEndMarker text then None
        else
            match tryActivateTask text with
            | Some task -> Some task
            | None -> current) None
