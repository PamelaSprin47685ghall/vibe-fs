module VibeFs.Opencode.ReviewTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.ReviewSession
open VibeFs.Kernel.Prompts
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Opencode.ReviewerLoop
open VibeFs.Opencode.ToolHelpers
open VibeFs.Mux.Wrappers
open VibeFs.Shell.ChildAgentRegistry

let private formatReviewResult = VibeFs.Kernel.Prompts.formatReviewResult

let submitReviewTool (registry: ChildAgentRegistry) (ctx: obj) (store: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    let client () = Dyn.get ctx "client"
    define "Submit completed work for the reviewer to accept or reject."
        (box {| report = strReq "Detailed report of what you did"; affectedFiles = strArrayOpt "Files you modified" |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            let sessionID = Dyn.str tc "sessionID"
            if sessionID = "" || not (store.isReviewActive sessionID) then
                resolveStr "You do not need review. Just continue with your work."
            elif not (store.tryLockReview sessionID) then
                resolveStr "A review is already in progress. Wait for it to finish."
            else
                let report = Dyn.str args "report"
                let affectedFiles =
                    if Dyn.isNullish (Dyn.get args "affectedFiles") then []
                    else Dyn.get args "affectedFiles" :?> obj array |> Array.map string |> List.ofArray
                let abort = Dyn.get tc "abortSignal"
                promise {
                    try
                        let task = defaultArg (store.getReviewTask sessionID) ""
                        let! result = runSubmitReview registry (client ()) store (Dyn.str tc "directory") sessionID report affectedFiles task abort
                        match result with
                        | Accepted
                        | Terminated ->
                            store.deactivateReview sessionID
                        | Rejected _ -> ()
                        return formatReviewResult result
                    finally
                        store.unlockReview sessionID
                })

let submitReviewResultTool (store: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    define "Submit your review verdict."
        (box {| feedback = strOpt "null to accept, or specific rejection feedback" |})
        (fun args context ->
            let sessionID =
                let id = Dyn.str context "sessionID"
                if id = "" then "loop" else id
            let result =
                match optStr args "feedback" with
                | None -> Accepted
                | Some f ->
                    let trimmed = f.Trim()
                    if trimmed = "" then Accepted else Rejected trimmed
            promise { return if store.resolvePendingReview (sessionID, result) then "Verdict submitted." else "No active review to resolve." })
