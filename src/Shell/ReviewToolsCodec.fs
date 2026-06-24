module VibeFs.Shell.ReviewToolsCodec

open VibeFs.Kernel.Domain
open VibeFs.Kernel.ReviewVerdict
open VibeFs.Shell.Dyn

type SubmitReviewArgs = {
    Report: string
    AffectedFiles: string list
}

type ReturnReviewerArgs = {
    Verdict: Verdict
    Feedback: string
}

let private strField (a: obj) (k: string) : string option =
    let v = Dyn.get a k
    if Dyn.isNullish v then None else Some(string v)

let private strListField (a: obj) (k: string) : string list =
    let v = Dyn.get a k
    if Dyn.isNullish v then []
    elif Dyn.isArray v then (v :?> obj array) |> Array.map string |> Array.toList
    else [ string v ]

let decodeSubmitReviewArgs (args: obj) : Result<SubmitReviewArgs, DomainError> =
    match strField args "report" with
    | None -> Error (InvalidIntent ("submit_review", "report", "must be a string"))
    | Some report ->
        Ok {
            Report = report
            AffectedFiles = strListField args "affectedFiles"
        }

let decodeReturnReviewerArgs (args: obj) : Result<ReturnReviewerArgs, DomainError> =
    match strField args "verdict" with
    | None -> Error (InvalidIntent ("return_reviewer", "verdict", "must be a string"))
    | Some raw ->
        match parseVerdict raw with
        | None -> Error (InvalidIntent ("return_reviewer", "verdict", "must be PASS or REJECT"))
        | Some verdict ->
            Ok {
                Verdict = verdict
                Feedback = defaultArg (strField args "feedback") ""
            }