module Wanxiangshu.Tests.ReviewToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.ReviewToolsCodec

let decodeSubmitReviewMissingReport () =
    let args = createObj [ "affectedFiles", box [| "a.fs" |] ]
    match decodeSubmitReviewArgs args with
    | Error (InvalidIntent ("submit_review", "report", _)) -> check "submit_review missing report" true
    | _ -> check "submit_review missing report" false

let decodeSubmitReviewOk () =
    let args = createObj [ "report", box "done"; "affectedFiles", box [| "x.fs"; "y.fs" |] ]
    match decodeSubmitReviewArgs args with
    | Ok sr ->
        check "submit_review ok report" (sr.Report = "done")
        equal "submit_review ok affected count" 2 sr.AffectedFiles.Length
    | Error _ -> check "submit_review ok" false

let decodeReturnReviewerInvalidVerdict () =
    let args = createObj [ "verdict", box "null" ]
    match decodeReturnReviewerArgs args with
    | Error (InvalidIntent ("return_reviewer", "verdict", _)) -> check "return_reviewer invalid verdict" true
    | _ -> check "return_reviewer invalid verdict" false

let decodeReturnReviewerReject () =
    let args = createObj [ "verdict", box "REJECT"; "feedback", box "fix tests" ]
    match decodeReturnReviewerArgs args with
    | Ok rr ->
        check "return_reviewer reject verdict" (rr.Verdict = Wanxiangshu.Kernel.ReviewVerdict.Reject)
        check "return_reviewer feedback" (rr.Feedback = "fix tests")
    | Error _ -> check "return_reviewer reject" false

let run () =
    decodeSubmitReviewMissingReport ()
    decodeSubmitReviewOk ()
    decodeReturnReviewerInvalidVerdict ()
    decodeReturnReviewerReject ()