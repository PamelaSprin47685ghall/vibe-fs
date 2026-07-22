module Wanxiangshu.Tests.ReviewToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.ReviewToolsCodec

let decodeSubmitReviewMissingReport () =
    let args = createObj [ "affectedFiles", box [| "a.fs" |] ]

    match decodeSubmitReviewArgs args with
    | Error(InvalidIntent("submit_review", "report", _)) -> check "submit_review missing report" true
    | _ -> check "submit_review missing report" false

let decodeSubmitReviewOk () =
    let args =
        createObj [ "report", box "done"; "affectedFiles", box [| "x.fs"; "y.fs" |] ]

    match decodeSubmitReviewArgs args with
    | Ok sr ->
        check "submit_review ok report" (sr.Report = "done")
        equal "submit_review ok affected count" 2 sr.AffectedFiles.Length
    | Error _ -> check "submit_review ok" false

let decodeReturnReviewerInvalidVerdict () =
    let args = createObj [ "verdict", box "null" ]

    match decodeReturnReviewerArgs args with
    | Error(InvalidIntent("return_reviewer", "verdict", _)) -> check "return_reviewer invalid verdict" true
    | _ -> check "return_reviewer invalid verdict" false

let decodeReturnReviewerRevise () =
    let args = createObj [ "verdict", box "REVISE"; "feedback", box "fix tests" ]

    match decodeReturnReviewerArgs args with
    | Ok rr ->
        check "return_reviewer revise verdict" (rr.Verdict = Wanxiangshu.Kernel.ReviewVerdict.Revise)
        check "return_reviewer feedback" (rr.Feedback = "fix tests")
    | Error _ -> check "return_reviewer revise" false

let decodeReturnReviewerReviseEmptyFeedbackRejected () =
    let args = createObj [ "verdict", box "REVISE"; "feedback", box "" ]

    match decodeReturnReviewerArgs args with
    | Error(InvalidIntent("return_reviewer", "feedback", msg)) ->
        check "return_reviewer REVISE empty feedback rejected" (msg.Contains "non-empty")
    | _ -> check "return_reviewer REVISE empty feedback rejected" false

let decodeReturnReviewerReviseMissingFeedbackRejected () =
    let args = createObj [ "verdict", box "REVISE" ]

    match decodeReturnReviewerArgs args with
    | Error(InvalidIntent("return_reviewer", "feedback", _)) ->
        check "return_reviewer REVISE missing feedback rejected" true
    | _ -> check "return_reviewer REVISE missing feedback rejected" false

let decodeReturnReviewerPerfectEmptyFeedbackRejected () =
    let args = createObj [ "verdict", box "PERFECT"; "feedback", box "" ]

    match decodeReturnReviewerArgs args with
    | Error(InvalidIntent("return_reviewer", "feedback", msg)) ->
        check "return_reviewer PERFECT empty feedback rejected" (msg.Contains "non-empty")
    | _ -> check "return_reviewer PERFECT empty feedback rejected" false

let decodeReturnReviewerPerfectWithFeedback () =
    let args = createObj [ "verdict", box "PERFECT"; "feedback", box "looks good" ]

    match decodeReturnReviewerArgs args with
    | Ok rr ->
        check "return_reviewer PERFECT verdict" (rr.Verdict = Wanxiangshu.Kernel.ReviewVerdict.Perfect)
        equal "return_reviewer PERFECT feedback" "looks good" rr.Feedback
    | Error _ -> check "return_reviewer PERFECT with feedback" false

let run () =
    decodeSubmitReviewMissingReport ()
    decodeSubmitReviewOk ()
    decodeReturnReviewerInvalidVerdict ()
    decodeReturnReviewerRevise ()
    decodeReturnReviewerReviseEmptyFeedbackRejected ()
    decodeReturnReviewerReviseMissingFeedbackRejected ()
    decodeReturnReviewerPerfectEmptyFeedbackRejected ()
    decodeReturnReviewerPerfectWithFeedback ()
