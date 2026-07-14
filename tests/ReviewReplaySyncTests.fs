module Wanxiangshu.Tests.ReviewReplaySyncTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Kernel.ReviewReplayPolicy
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types

let textsFromFlatPartsIncludesToolOutput () =
    let toolState =
        { status = fromString "completed"
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

let reviewerOnlyHistoryDoesNotActivateLoop () =
    let texts =
        [ Wanxiangshu.Kernel.ReviewPrompts.Submission.reviewerPrompt "ship feature" "" [] ]

    equal "reviewer original_task only must not activate worker loop" None (inferReviewTaskFromTexts texts)

let run () =
    textsFromFlatPartsIncludesToolOutput ()
    truncatedTextsLoseAnchor ()
    fullTextsRecoverAnchor ()
    reviewerOnlyHistoryDoesNotActivateLoop ()
