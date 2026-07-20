module Wanxiangshu.Hosts.Opencode.ReviewerLoop

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewSession.StateMachine
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Hosts.Opencode.SessionIo
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.SubagentSpawn
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality

/// Create a reviewer child session under the given parent, register it, and
/// return the child id (empty string on failure).
let createReviewerChild
    (registry: ChildAgentRegistry)
    (client: obj)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (directory: string)
    (parentID: string option)
    (sessionID: string)
    (title: string)
    : JS.Promise<string> =
    promise {
        let createBody =
            box
                {| query = box {| directory = directory |}
                   body =
                    box
                        {| parentID =
                            match parentID with
                            | Some p -> box p
                            | None -> box null
                           title = title |} |}

        match getSessionApiFromClient client with
        | Error _ -> return ""
        | Ok session ->
            let! createResult = invoke1 createBody "create" session
            let childID = Dyn.str (Dyn.get createResult "data") "id"

            if childID <> "" then
                reviewStore.addChild (sessionID, childID)
                registry.RegisterChildAgent(childID, "reviewer", parentID)

            return childID
    }

/// Run the reviewer prompt-nudge loop on an existing child session: prompt with
/// the review instructions, wait for the verdict via return_reviewer, nudging
/// up to `maxNudges` times if the reviewer hasn't submitted.  Loop control is
/// delegated to the pure `decideAfterRound` / `promptParts` primitives in
/// `Kernel.ReviewSession`.
let runReviewerLoop
    (registry: ChildAgentRegistry)
    (client: obj)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (directory: string)
    (childID: string)
    (initialParts: string list)
    (abortSignal: obj)
    : JS.Promise<ReviewResult> =
    ReviewerLoopOps.runLoopWithCleanup
        registry
        client
        reviewStore
        directory
        childID
        initialParts
        abortSignal

/// Run a reviewer session: create a reviewer child, prompt it with review
/// instructions + task, wait for the verdict.
let runReviewerSession
    (registry: ChildAgentRegistry)
    (client: obj)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (directory: string)
    (sessionID: string)
    (task: string)
    : JS.Promise<ReviewResult> =
    promise {
        let parentID =
            registry.ResolveSubsessionParentID(if sessionID = "" then None else Some sessionID)

        let! childID = createReviewerChild registry client reviewStore directory parentID sessionID "Pre-Reviewer"

        if childID = "" then
            return Terminated
        else
            let parts = [ reviewerPrompt task "" [] ]
            return! runReviewerLoop registry client reviewStore directory childID parts null
    }

/// Run a submit-review (used by the submit_review tool): create a reviewer
/// child, prompt it with review instructions + change report + affected files +
/// original task, wait for the verdict.
let runSubmitReview
    (registry: ChildAgentRegistry)
    (client: obj)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (directory: string)
    (sessionID: string)
    (report: string)
    (affectedFiles: string list)
    (task: string)
    (abortSignal: obj)
    : JS.Promise<ReviewResult> =
    promise {
        let parentID = registry.ResolveSubsessionParentID(Some sessionID)
        let! childID = createReviewerChild registry client reviewStore directory parentID sessionID "Reviewer"

        if childID = "" then
            return Terminated
        else
            let parts = [ reviewerPrompt task report affectedFiles ]
            return! runReviewerLoop registry client reviewStore directory childID parts abortSignal
    }
