module VibeFs.Kernel.LoopMessages

open VibeFs.Kernel.PromptFrontMatter

/// Structured front-matter field names and verdict values are the single source
/// of truth shared by every producer — slash-command activation, the
/// submit_review rendering in `Prompts.formatReviewResult`, and loop
/// cancellation — and by the reconstruction fold `inferReviewTaskFromTexts`
/// that replays them after an opencode restart. A normal user message
/// practically never carries a YAML front-matter block, so these fields are a
/// collision-free anchor: far safer than scanning prose for marker substrings.
let taskField = "task"
let verdictField = "verdict"
let verdictAccepted = "accepted"
let verdictRejected = "rejected"
let verdictTerminated = "terminated"
let verdictCancelled = "cancelled"

/// Verdicts that END With-Review Mode. Reject/terminate keep it active (the work
/// continues), so they are deliberately excluded.
let isEndVerdict (verdict: string) : bool =
    verdict = verdictAccepted || verdict = verdictCancelled

let loopFooter =
    [ "- report: a detailed description of what you did and why"
      "- affectedFiles: list of every file you modified or created"
      ""
      "A reviewer will examine your submission. If accepted, you are done. If rejected, you will receive specific feedback to address." ]

let buildLoopMessage (task: string) (bodyLines: string list) : string =
    frontMatterPrompt [ yamlScalarField taskField task ] (String.concat "\n" (bodyLines @ loopFooter))

/// Loop cancellation carries a `verdict: cancelled` front-matter anchor so a
/// restart replay recognizes it structurally, followed by the human-readable
/// line. Authored once here, consumed verbatim by both hosts' cancel paths.
let loopCancelledMessage : string =
    frontMatterPrompt [ yamlScalarField verdictField verdictCancelled ] "With-Review Mode cancelled."

// ── Reconstruction from dialogue history ─────────────────────────────────────

/// After an opencode restart the in-memory review store is gone, but the
/// dialogue history still carries the loop messages this module and
/// `Prompts.formatReviewResult` author. This pure fold replays them to recover
/// the current review task: the history is the single source of truth and the
/// store is merely a re-buildable projection of it.
///
/// Each fragment is matched on its structured front-matter ONLY:
///   `task` field         -> (re)activate with that task
///   END verdict          -> accepted / cancelled clear the task
///   reject/terminate verdict, or any non-front-matter prose -> task untouched
let inferReviewTaskFromTexts (texts: string seq) : string option =
    texts
    |> Seq.fold (fun current text ->
        let fields = parseFrontMatterScalars text
        match Map.tryFind taskField fields with
        | Some task when task <> "" -> Some task
        | _ ->
            match Map.tryFind verdictField fields with
            | Some verdict when isEndVerdict verdict -> None
            | _ -> current) None
