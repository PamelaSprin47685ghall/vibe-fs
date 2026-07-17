module Wanxiangshu.Runtime.LoopMessages

open Wanxiangshu.Runtime.PromptFrontMatter

/// Structured front-matter field names and verdict values are the single source
/// of truth shared by every producer — slash-command activation, the
/// submit_review rendering in `Prompts.formatReviewResult`, and loop
/// cancellation — and by the reconstruction fold `inferReviewTaskFromTexts`
/// that replays them after an opencode restart. A normal user message
/// practically never carries a YAML front-matter block, so these fields are a
/// collision-free anchor: far safer than scanning prose for marker substrings.
let taskField = "task"

/// Worker With-Review activation only. Reviewer prompts carry the parent's
/// requirement under this key so `inferReviewTaskFromTexts` never re-activates
/// a reviewer child session from its own dialogue history.
let originalTaskField = "original_task"
let verdictField = "verdict"
let verdictAccepted = "accepted"
let verdictNeedsRevision = "needs_revision"
let verdictTerminated = "terminated"
let verdictCancelled = "cancelled"
let commandField = "command"
let commandWithReview = "with-review"
let commandWithReviewPrecheck = "with-review-precheck"

/// Verdicts that END With-Review Mode. needs_revision/terminate keep it active (the work
/// continues), so they are deliberately excluded.
let isEndVerdict = Wanxiangshu.Kernel.Review.ReviewVerdictWire.isEndVerdict

let loopFooter =
    [ "- report: a detailed description of what you did and why"
      "- affectedFiles: list of every file you modified or created"
      "- wip (optional, defaults to true): omit or true while the task is not fully complete; false only when the full task is done"
      ""
      "You must fully complete every item in the task — no shortcuts, no reduced scope, no deferred work."
      "A reviewer will examine your submission. If accepted, you are done. If revision is requested, you will receive specific feedback to address." ]

let buildLoopMessage (task: string) (bodyLines: string list) : string =
    frontMatterPrompt [ yamlField taskField task ] (String.concat "\n" (bodyLines @ loopFooter))

let buildLoopCommandTemplate (commandName: string) (bodyLines: string list) : string =
    frontMatterPrompt [ yamlField commandField commandName ] (String.concat "\n" bodyLines)

/// Loop cancellation carries a `verdict: cancelled` front-matter anchor so a
/// restart replay recognizes it structurally, followed by the human-readable
/// line. Authored once here, consumed verbatim by both hosts' cancel paths.
let loopCancelledMessage: string =
    frontMatterPrompt [ yamlField verdictField verdictCancelled ] "With-Review Mode cancelled."

// ── Reconstruction from dialogue history ─────────────────────────────────────

/// Parse *text* into one scalar map per front-matter block (order preserved).
/// Each block is parsed independently so multi-front-matter blocks do not merge.
let frontMatterScalarBlocks (text: string) : Map<string, string> list = parseFrontMatterScalarBlocks text

/// After an opencode restart the in-memory review store is gone, but the
/// dialogue history still carries the loop messages this module and
/// `Prompts.formatReviewResult` author. This pure fold replays them to recover
/// the current review task: the history is the single source of truth and the
/// store is merely a re-buildable projection of it.
///
/// Processing is block-ordered within each text fragment: a `task` field
/// (re)activates the current task; an end-verdict (`accepted`/`cancelled`)
/// clears it.  Later blocks within the same text can override or cancel
/// earlier blocks — this is the fix for multi-front-matter where block 1
/// activates a task and block 2 cancels it.
///
/// Each fragment is matched on its structured front-matter ONLY:
///   `task` field         -> (re)activate with that task (worker With-Review only;
///                          reviewer prompts use `original_task` instead)
///   END verdict          -> accepted / cancelled clear the task
///   needs_revision/terminate verdict, or any non-front-matter prose -> task untouched
let inferReviewTaskFromTexts (texts: string seq) : string option =
    texts
    |> Seq.fold
        (fun current text ->
            frontMatterScalarBlocks text
            |> List.fold
                (fun currentBlock fields ->
                    match Map.tryFind taskField fields with
                    | Some task when task <> "" -> Some task
                    | _ ->
                        match Map.tryFind verdictField fields with
                        | Some verdict when isEndVerdict verdict -> None
                        | _ -> currentBlock)
                current)
        None

let doubleCheckField = "double-check"

let hasDoubleCheckAnchor (texts: string seq) : bool =
    texts
    |> Seq.exists (fun text -> Map.containsKey doubleCheckField (parseFrontMatterScalars text))
