module Wanxiangshu.Omp.ReviewLoop

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.ReviewRuntime

module Dyn = Wanxiangshu.Shell.Dyn

type JsReviewResult =
    { accepted: bool option
      feedback: string option
      terminated: bool option }

let private terminatedResult fb =
    { accepted = Some false
      feedback = Some fb
      terminated = Some true }

let private createDeferred () : JS.Promise<JsReviewResult> * (JsReviewResult -> unit) =
    let d = emitJsExpr () "Promise.withResolvers()"
    unbox (Dyn.get d "promise"), unbox (Dyn.get d "resolve")

let private attachReviewChild
    (store: ReviewStore)
    (parentId: string)
    (childId: string)
    (onResolve: JsReviewResult -> unit)
    (childSession: obj)
    =
    store.addChild (parentId, childId)

    store.setPendingReview (
        childId,
        fun kr ->
            try
                Dyn.callMethod0 childSession "abort" |> ignore
            with _ ->
                ()

            let js =
                match kr with
                | Accepted fb ->
                    { accepted = Some true
                      feedback = (if fb = "" then None else Some fb)
                      terminated = None }
                | NeedsRevision fb ->
                    { accepted = Some false
                      feedback = Some fb
                      terminated = None }
                | Terminated -> terminatedResult "Review session closed."

            onResolve js
    )

let private detachReviewChild (store: ReviewStore) (_parentId: string) (childId: string) =
    store.resolvePendingReview (childId, Terminated) |> ignore
    store.unlockReview childId

let private maxNudges = 3

let private executeNudgeCheck (childSession: obj) : JS.Promise<unit> =
    childSession?prompt (reviewerNudgePrompt) |> unbox<JS.Promise<unit>>

/// Event-driven nudge loop: races the review-resolution promise against each
/// prompt's completion.  When the prompt wins (model finished its turn without
/// resolving the review), a nudge is dispatched.  No timers.
let private runNudgeLoop
    (childSession: obj)
    (resolvedPromise: JS.Promise<JsReviewResult>)
    (currentPrompt: JS.Promise<unit>)
    (onMaxNudges: unit -> JsReviewResult)
    : JS.Promise<JsReviewResult> =
    let rec loop nudgeCount promptPromise =
        promise {
            let! winner =
                Promise.race
                    [ resolvedPromise |> Promise.map Some
                      promptPromise |> Promise.map (fun () -> None) ]

            match winner with
            | Some result -> return result
            | None ->
                if nudgeCount >= maxNudges then
                    return onMaxNudges ()
                else
                    let nextPrompt = executeNudgeCheck childSession
                    return! loop (nudgeCount + 1) nextPrompt
        }

    loop 0 currentPrompt

let runReviewLoop
    (scope: RuntimeScope)
    (pi: obj)
    (ctx: obj)
    (store: ReviewStore)
    (parentId: string)
    (report: string)
    (files: string array)
    (task: string option)
    : JS.Promise<JsReviewResult> =
    promise {
        let resolvedPromise, resolveReview = createDeferred ()
        let! child = createChildSession scope pi ctx ompReviewChildToolNames None [||] None
        let childSession = child.session
        let childCtx = createObj [ "sessionManager", Dyn.get childSession "sessionManager" ]
        let childId = getSessionIdFromContext childCtx |> Option.defaultValue ""

        let cleanupChild () =
            detachReviewChild store parentId childId

            if not (Dyn.isNullish (Dyn.get childSession "abort")) then
                try
                    Dyn.callMethod0 childSession "abort" |> ignore
                with _ ->
                    ()

            child.dispose |> Option.iter (fun dispose -> dispose ())

        if childId = "" then
            if not (Dyn.isNullish (Dyn.get childSession "abort")) then
                try
                    Dyn.callMethod0 childSession "abort" |> ignore
                with _ ->
                    ()

            child.dispose |> Option.iter (fun dispose -> dispose ())
            return terminatedResult "Review child session unavailable"
        else
            attachReviewChild store parentId childId (fun r -> resolveReview r) childSession
            let initial = buildOmpReviewInitialPrompt report (files |> Array.toList) task
            let initialPrompt = childSession?prompt (initial) |> unbox<JS.Promise<unit>>

            let onMax () =
                let sm = unbox<ISessionManager> (Dyn.get childSession "sessionManager")

                let fb =
                    readAssistantText sm 0 "\n\n"
                    |> Option.defaultValue "Reviewer failed to finish."

                terminatedResult fb

            let! outcome = runNudgeLoop childSession resolvedPromise initialPrompt onMax
            cleanupChild ()
            return outcome
    }

let private jsToReviewResult (js: JsReviewResult) : ReviewResult =
    if defaultArg js.terminated false then
        Terminated
    elif js.accepted = Some true then
        Accepted(defaultArg js.feedback "")
    elif js.accepted = Some false then
        NeedsRevision(defaultArg js.feedback "")
    else
        Terminated

let runPreReviewerSession
    (scope: RuntimeScope)
    (pi: obj)
    (ctx: obj)
    (store: ReviewStore)
    (task: string)
    : JS.Promise<ReviewResult> =
    promise {
        let taskTrim = task.Trim()

        if taskTrim = "" then
            return Terminated
        else
            match getSessionIdFromContext ctx with
            | None -> return Terminated
            | Some parentId ->
                let resolvedPromise, resolveReview = createDeferred ()
                let! child = createChildSession scope pi ctx ompReviewChildToolNames None [||] None
                let childSession = child.session
                let childCtx = createObj [ "sessionManager", Dyn.get childSession "sessionManager" ]
                let childId = getSessionIdFromContext childCtx |> Option.defaultValue ""

                let cleanupChild () =
                    detachReviewChild store parentId childId

                    if not (Dyn.isNullish (Dyn.get childSession "abort")) then
                        try
                            Dyn.callMethod0 childSession "abort" |> ignore
                        with _ ->
                            ()

                    child.dispose |> Option.iter (fun dispose -> dispose ())

                if childId = "" then
                    cleanupChild ()
                    return Terminated
                else
                    attachReviewChild store parentId childId (fun r -> resolveReview r) childSession
                    let initial = reviewerPrompt taskTrim "" []
                    let initialPrompt = childSession?prompt (initial) |> unbox<JS.Promise<unit>>

                    let! jsOutcome =
                        runNudgeLoop childSession resolvedPromise initialPrompt (fun () ->
                            terminatedResult "Pre-review timed out.")

                    cleanupChild ()
                    return jsToReviewResult jsOutcome
    }
