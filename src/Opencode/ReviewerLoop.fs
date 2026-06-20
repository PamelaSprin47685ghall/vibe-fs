module VibeFs.Opencode.ReviewerLoop

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.ReviewSession
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Opencode.SessionIo

let private maxNudges = 3

/// Create a reviewer child session under the given parent, register it, and
/// return the child id (empty string on failure).
let createReviewerChild (registry: ChildAgentRegistry) (client: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                        (directory: string) (parentID: string option)
                        (sessionID: string) (title: string) : JS.Promise<string> =
    promise {
        let createBody =
            box {|
                query = box {| directory = directory |}
                body = box {|
                    parentID =
                        match parentID with
                        | Some p -> box p
                        | None -> box null
                    title = title
                |}
            |}
        let! createResult = invoke1 createBody "create" (Dyn.get client "session")
        let childID = Dyn.str (Dyn.get createResult "data") "id"
        if childID <> "" then
            reviewStore.addChild(sessionID, childID)
            registry.RegisterChildAgent(childID, "reviewer", parentID)
        return childID
    }

let private textParts (parts: string list) : obj array =
    parts |> List.map (fun text -> box {| ``type`` = "text"; text = text |}) |> Array.ofList

/// Run the reviewer prompt-nudge loop on an existing child session: prompt with
/// the review instructions, wait for the verdict via return_reviewer, nudging
/// up to `maxNudges` times if the reviewer hasn't submitted.  Loop control is
/// delegated to the pure `decideAfterRound` / `promptParts` primitives in
/// `Kernel.ReviewSession`.
let runReviewerLoop (client: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                    (childID: string) (initialParts: string list) (abortSignal: obj)
                    : JS.Promise<ReviewResult> =
    promise {
        let verdict : ReviewResult option ref = ref None
        reviewStore.setPendingReview(childID, (fun r -> verdict.Value <- Some r))
        reviewStore.tryLockReview childID |> ignore
        let runRound (parts: string list) =
            promise {
                let promptBody =
                    box {|
                        path = box {| id = childID |}
                        body = box {| agent = "reviewer"; parts = textParts parts; tools = box (createObj [ "return_reviewer", box true ]) |}
                    |}
                try
                    do! promptWithAbort client promptBody abortSignal
                    match verdict.Value with
                    | Some v -> return Resolved v
                    | None -> return NoResult
                with _ ->
                    return PromptFailed
            }
        let rec loop nudgeCount =
            promise {
                let parts = promptParts nudgeCount initialParts reviewerNudgePrompt
                let! outcome = runRound parts
                match decideAfterRound nudgeCount outcome maxNudges with
                | Finish result -> return result
                | Nudge next -> return! loop next
            }
        let! result = loop 0
        reviewStore.unlockReview childID
        return result
    }

/// Run a pre-review session (used by /loop-review): create a reviewer child,
/// prompt it with review instructions + task, wait for the verdict.
let runReviewerSession (registry: ChildAgentRegistry) (client: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                       (directory: string) (sessionID: string) (task: string)
                       : JS.Promise<ReviewResult> =
    promise {
        let parentID = registry.ResolveSubsessionParentID(if sessionID = "" then None else Some sessionID)
        let! childID = createReviewerChild registry client reviewStore directory parentID sessionID "Pre-Reviewer"
        if childID = "" then return Terminated
        else
            let parts = [ reviewInstructions; $"=== Task ===\n\n{task}" ]
            return! runReviewerLoop client reviewStore childID parts null
    }

/// Run a submit-review (used by the submit_review tool): create a reviewer
/// child, prompt it with review instructions + change report + affected files +
/// original task, wait for the verdict.
let runSubmitReview (registry: ChildAgentRegistry) (client: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                    (directory: string) (sessionID: string)
                    (report: string) (affectedFiles: string list)
                    (task: string) (abortSignal: obj)
                    : JS.Promise<ReviewResult> =
    promise {
        let parentID = registry.ResolveSubsessionParentID(Some sessionID)
        let! childID = createReviewerChild registry client reviewStore directory parentID sessionID "Reviewer"
        if childID = "" then return Terminated
        else
            let filesText = String.concat "\n" affectedFiles
            let parts =
                [ reviewInstructions
                  $"=== Change Report ===\n\n{report}"
                  $"=== Affected Files ===\n\n{filesText}"
                  if task <> "" then $"=== Original Task ===\n\n{task}" ]
            return! runReviewerLoop client reviewStore childID parts abortSignal
    }
