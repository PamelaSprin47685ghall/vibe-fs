module Wanxiangshu.Tests.ReviewReplaySyncTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ReviewReplayPolicy
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.ReviewReplaySync

let textsFromFlatPartsIncludesToolOutput () =
    let toolState =
        { status = "completed"
          output = "tool-body"
          error = ""
          input = ()
          operationAction = "" }
    let msg =
        { info =
            { id = "m1"
              sessionID = "s1"
              role = Assistant
              agent = ""
              isError = false
              toolName = ""
              details = ()
              time = () }
          parts = [ ToolPart("read", "c1", Some toolState, ()) ]
          source = Native
          raw = () }
    let flat = flatten [ msg ]
    let texts = textsFromFlatParts flat |> Seq.toList
    equal "tool output collected" [ "tool-body" ] texts

let syncReviewFromTextsActivatesFromTexts () =
    let store = createReviewStore ()
    let activate =
        frontMatterPrompt [ yamlField taskField "from-replay" ] "body"
    syncReviewFromTexts store "s2" [ activate ]
    equal "replay activates task" (Some "from-replay") (store.getReviewTask "s2")
    check "replay marks session active" (store.isReviewActive "s2")

let syncReviewFromTextsDeactivatesOnEndVerdict () =
    let store = createReviewStore ()
    store.activateReview ("s3", "active-task", 1L)
    let accept = Wanxiangshu.Kernel.ReviewPrompts.formatReviewResult (Accepted "")
    syncReviewFromTexts store "s3" [ accept ]
    check "end verdict deactivates review" (not (store.isReviewActive "s3"))

let syncReviewFromTextsPreservesActiveOnReject () =
    let store = createReviewStore ()
    let activate = buildLoopMessage "active-task" [ "With-Review Mode is active." ]
    let rejected = Wanxiangshu.Kernel.ReviewPrompts.formatReviewResult (Rejected "fix tests")
    syncReviewFromTexts store "s4" [ activate; rejected ]
    equal "reject keeps task active" (Some "active-task") (store.getReviewTask "s4")
    check "reject keeps session active" (store.isReviewActive "s4")

/// Regression: when the task: anchor is missing from replay texts (e.g. truncated
/// by compaction), inferReviewTaskFromTexts correctly returns None — proving the
/// bug is in the DATA source, not the fold logic.
let truncatedTextsLoseAnchor () =
    let texts = [ "some prose without front matter" ]
    equal "missing anchor → None" None (inferReviewTaskFromTexts texts)

/// Regression: full texts with the anchor correctly recover the task.
let fullTextsRecoverAnchor () =
    let activate = buildLoopMessage "ship feature" [ "With-Review Mode is active." ]
    let texts = [ activate; "working on it" ]
    equal "full texts with anchor → Some" (Some "ship feature") (inferReviewTaskFromTexts texts)

let run () =
    textsFromFlatPartsIncludesToolOutput ()
    syncReviewFromTextsActivatesFromTexts ()
    syncReviewFromTextsDeactivatesOnEndVerdict ()
    syncReviewFromTextsPreservesActiveOnReject ()
    truncatedTextsLoseAnchor ()
    fullTextsRecoverAnchor ()
