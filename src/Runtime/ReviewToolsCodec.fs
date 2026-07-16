module Wanxiangshu.Runtime.ReviewToolsCodec

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ReviewVerdict
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.DynField

type SubmitReviewArgs =
    { Report: string
      AffectedFiles: string list
      Wip: bool option }

type ReturnReviewerArgs = { Verdict: Verdict; Feedback: string }

let private strListField (a: obj) (k: string) : string list =
    let v = Dyn.get a k

    if Dyn.isNullish v then
        []
    elif Dyn.isArray v then
        (v :?> obj array) |> Array.map string |> Array.toList
    else
        [ string v ]

let decodeSubmitReviewArgs (args: obj) : Result<SubmitReviewArgs, DomainError> =
    match strField args "report" with
    | None -> Error(InvalidIntent("submit_review", "report", "must be a string"))
    | Some report ->
        Ok
            { Report = report
              AffectedFiles = strListField args "affectedFiles"
              Wip = optBool args "wip" }

let decodeReturnReviewerArgs (args: obj) : Result<ReturnReviewerArgs, DomainError> =
    match strField args "verdict" with
    | None -> Error(InvalidIntent("return_reviewer", "verdict", "must be a string"))
    | Some raw ->
        match parseVerdict raw with
        | None -> Error(InvalidIntent("return_reviewer", "verdict", "must be PERFECT or REVISE"))
        | Some verdict ->
            Ok
                { Verdict = verdict
                  Feedback = defaultArg (strField args "feedback") "" }
