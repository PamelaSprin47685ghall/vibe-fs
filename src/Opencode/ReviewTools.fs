module VibeFs.Opencode.ReviewTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.ReviewSession
open VibeFs.Kernel.ReviewVerdict
open VibeFs.Kernel.ReviewPrompts
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.ToolCatalog
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Opencode.ReviewerLoop
open VibeFs.Opencode.ToolHelpers
open VibeFs.Mux.Wrappers
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.Dyn

let private formatReviewResult = VibeFs.Kernel.ReviewPrompts.formatReviewResult

let submitReviewTool (registry: ChildAgentRegistry) (ctx: obj) (store: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    let client () = Dyn.get ctx "client"
    define "Submit completed work for the reviewer to accept or reject."
        (box {| report = strReq "Detailed report of what you did"; affectedFiles = strArrayOpt "Files you modified"; wip = boolOpt Params.submitReviewWip |})
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
                let wip = submitReviewIsWip (optBool args "wip")
                promise {
                    try
                        if wip then
                            return submitReviewWipAcknowledgment
                        else
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

let submitReviewResultTool (ctx: obj) (store: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    let client () = Dyn.get ctx "client"
    let pluginDirectory = Dyn.str ctx "directory"
    define "Submit your review verdict."
        (box {| verdict = enumReq [| "PASS"; "REJECT" |] "PASS to accept, REJECT to reject"
                feedback = strOpt "detailed, actionable feedback when rejecting; omit when passing" |})
        (fun args context ->
            let sessionID =
                let id = Dyn.str context "sessionID"
                if id = "" then "loop" else id
            let directory =
                let d = Dyn.str context "directory"
                if d <> "" then d else pluginDirectory
            let feedback = defaultArg (optStr args "feedback") ""
            promise {
                match parseVerdict (Dyn.str args "verdict") with
                | None -> return reviewerNudgePrompt
                | Some verdict ->
                    let! texts = VibeFs.Opencode.SessionIo.readSessionTexts (client ()) sessionID directory
                    let doubleCheckDone = hasDoubleCheckAnchor texts
                    match decideReviewSubmission verdict feedback doubleCheckDone with
                    | AskDoubleCheck ->
                        let task = defaultArg (inferReviewTaskFromTexts texts) ""
                        return doubleCheckPrompt task
                    | Finalize result ->
                        return if store.resolvePendingReview (sessionID, result) then "Verdict submitted." else "No active review to resolve."
            })
