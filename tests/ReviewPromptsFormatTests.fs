module Wanxiangshu.Tests.ReviewPromptsFormatTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewPrompts.Format
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.PromptFrontMatter
open Fable.Core.JsInterop

// ── submitReviewIsWip ────────────────────────────────────────────────────────

let submitReviewIsWipNoneDefaultsTrue () =
    check "None defaults to true" (submitReviewIsWip None = true)

let submitReviewIsWipSomeTrue () =
    check "Some true returns true" (submitReviewIsWip (Some true))

let submitReviewIsWipSomeFalse () =
    check "Some false returns false" (not (submitReviewIsWip (Some false)))

// ── submitReviewWipAcknowledgment ────────────────────────────────────────────

let submitReviewWipAcknowledgmentNonEmpty () =
    let msg = submitReviewWipAcknowledgment
    check "acknowledgment is non-empty" (msg <> "")
    check "acknowledgment mentions With-Review Mode" (msg.Contains "With-Review Mode")

// ── formatReviewResult ───────────────────────────────────────────────────────

let formatReviewResultAcceptedNoFeedback () =
    let result = Accepted ""
    let text = formatReviewResult result
    check "accepted no feedback contains accepted verdict" (text.Contains "accepted")
    check "accepted no feedback contains Review passed" (text.Contains "Review passed")
    check "accepted no feedback contains With-Review Mode has ended" (text.Contains "With-Review Mode has ended")

let formatReviewResultAcceptedWithFeedback () =
    let feedback = "Good work, minor style nit."
    let result = Accepted feedback
    let text = formatReviewResult result
    check "accepted with feedback contains verdict" (text.Contains "accepted")
    check "accepted with feedback contains feedback text" (text.Contains feedback)

    check
        "accepted with feedback contains Review passed with feedback"
        (text.Contains "Review passed with the following feedback")

let formatReviewResultNeedsRevision () =
    let feedback = "Fix the boundary checks."
    let result = NeedsRevision feedback
    let text = formatReviewResult result
    check "needs_revision contains verdict" (text.Contains "needs_revision")
    check "needs_revision contains feedback" (text.Contains feedback)
    check "needs_revision keeps With-Review Mode active" (text.Contains "With-Review Mode is still active")

let formatReviewResultTerminated () =
    let result = Terminated
    let text = formatReviewResult result
    check "terminated contains verdict" (text.Contains "terminated")
    check "terminated no verdict label in prose" (text.Contains "Review terminated without verdict")
    check "terminated keeps With-Review Mode active" (text.Contains "With-Review Mode is still active")

// ── parseFrontMatter / multi-frontmatter ──────────────────────────────────────

let multiFrontMatterInput =
    "---\nauthor: Alice\nmode: review\n---\n---\nauthor: Bob\npriority: high\n---\nBody text after all frontmatter."

let parseMultiFrontMatterMerging () =
    let parsed = parseFrontMatter multiFrontMatterInput
    check "parseFrontMatter returns non-null for multi-frontmatter input" (not (isNull parsed))
    // later block overrides earlier — Bob overrides Alice
    check "author from later block wins" (parsed?("author") = box "Bob")
    check "mode from first block preserved" (parsed?("mode") = box "review")
    check "priority from second block present" (parsed?("priority") = box "high")

let parseMultiFrontMatterScalars () =
    let scalars = parseFrontMatterScalars multiFrontMatterInput
    check "scalars non-empty" (scalars.Count > 0)
    check "author scalar from later block" (scalars.TryFind "author" = Some "Bob")
    check "mode scalar from first block" (scalars.TryFind "mode" = Some "review")
    check "priority scalar from second block" (scalars.TryFind "priority" = Some "high")

let bodyAfterMultiFrontMatter () =
    let body = bodyAfterFrontMatter multiFrontMatterInput
    check "body is non-empty" (body <> "")
    check "body starts with expected text" (body.StartsWith "Body text")
    check "body does not contain frontmatter delimiter" (not (body.Contains "---"))

// ── extractFrontMatterFenceStrings / renderCompactionAnchorPrompt ──────────────

let multiFrontMatterExtractionToFenceStrings () =
    let input =
        "---\ntask: Ship feature\n---\n---\nauthor: Alice\nmode: review\n---\n---\nsource: compaction-anchor\n---\n---\nsquad_event: tasks_created\nsession_id: s1\n---\n---\nverdict: accepted\n---\nBody."

    let fences = extractFrontMatterFenceStrings input
    equal "whitelist keeps task, squad_event, verdict" 3 (List.length fences)
    check "first fence has task" (fences.[0].Contains "task")
    check "second fence has squad_event" (fences.[1].Contains "squad_event")
    check "third fence has verdict" (fences.[2].Contains "verdict")

    check
        "no compaction marker in fences"
        (not (List.exists (fun (s: string) -> s.Contains "compaction-anchor") fences))

    check "no non-whitelist blocks" (not (List.exists (fun (s: string) -> s.Contains "Alice") fences))

let compactionAnchorPromptRendersMarkerAndBody () =
    let fence1 = "---\nauthor: Alice\n---"
    let fence2 = "---\nauthor: Bob\n---"
    let prompt = renderCompactionAnchorPrompt [ fence1; fence2 ]
    check "prompt contains body" (prompt.Contains "See above for some messages before compaction.")

    let fenceCount =
        prompt.Split([| "---" |], System.StringSplitOptions.None).Length - 1

    check "prompt has two fences" (fenceCount >= 2)

let compactionAnchorPromptEmptyFencesReturnsEmpty () =
    equal "empty fences returns empty string" "" (renderCompactionAnchorPrompt [])

let run () =
    submitReviewIsWipNoneDefaultsTrue ()
    submitReviewIsWipSomeTrue ()
    submitReviewIsWipSomeFalse ()
    submitReviewWipAcknowledgmentNonEmpty ()
    formatReviewResultAcceptedNoFeedback ()
    formatReviewResultAcceptedWithFeedback ()
    formatReviewResultNeedsRevision ()
    formatReviewResultTerminated ()
    parseMultiFrontMatterMerging ()
    parseMultiFrontMatterScalars ()
    bodyAfterMultiFrontMatter ()
    multiFrontMatterExtractionToFenceStrings ()
    compactionAnchorPromptRendersMarkerAndBody ()
    compactionAnchorPromptEmptyFencesReturnsEmpty ()
