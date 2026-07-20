module Wanxiangshu.Hosts.Omp.ReviewLoop

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ReviewRuntime

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Hosts.Omp.ChildCleanup
open Wanxiangshu.Runtime.OmpHostBindings

let private createDeferred () : JS.Promise<ReviewResult> * (ReviewResult -> unit) =
    let d = emitJsExpr () "Promise.withResolvers()"
    unbox (Dyn.get d "promise"), unbox (Dyn.get d "resolve")

let private abortChildHost (childSession: obj) : unit =
    // Single-path physical abort; ignore result (cleanup best-effort).
    abortOnce childSession (box null) ""
    |> Promise.map ignore
    |> Promise.catch (fun _ -> ())
    |> Promise.start

let private attachReviewChild
    (store: ReviewStore)
    (parentId: string)
    (childId: string)
    (onResolve: ReviewResult -> unit)
    (childSession: obj)
    =
    store.addChild (parentId, childId)

    store.setPendingReview (
        childId,
        fun kr ->
            abortChildHost childSession
            onResolve kr
    )

let private detachReviewChild (store: ReviewStore) (_parentId: string) (childId: string) (childSession: obj) =
    abortChildHost childSession
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
    (resolvedPromise: JS.Promise<ReviewResult>)
    (currentPrompt: JS.Promise<unit>)
    (onMaxNudges: unit -> ReviewResult)
    : JS.Promise<ReviewResult> =
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
    : JS.Promise<ReviewResult> =
    promise {
        let resolvedPromise, resolveReview = createDeferred ()
        let! child = createChildSession scope pi ctx ompReviewChildToolNames None [||] None
        let childSession = child.session
        let childId = child.childId

        let cleanupChild () =
            detachReviewChild store parentId childId childSession
            CleanupChildSession childSession child.dispose

        try
            if childId = "" then
                cleanupChild ()
                return Terminated
            else
                attachReviewChild store parentId childId (fun r -> resolveReview r) childSession
                let initial = buildOmpReviewInitialPrompt report (files |> Array.toList) task
                let initialPrompt = childSession?prompt (initial) |> unbox<JS.Promise<unit>>
                let! outcome = runNudgeLoop childSession resolvedPromise initialPrompt (fun () -> Terminated)
                cleanupChild ()
                return outcome
        with _ ->
            cleanupChild ()
            return Terminated
    }

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
                let childId = child.childId

                let cleanupChild () =
                    detachReviewChild store parentId childId childSession
                    CleanupChildSession childSession child.dispose

                try
                    if childId = "" then
                        cleanupChild ()
                        return Terminated
                    else
                        attachReviewChild store parentId childId (fun r -> resolveReview r) childSession
                        let initial = reviewerPrompt taskTrim "" []
                        let initialPrompt = childSession?prompt (initial) |> unbox<JS.Promise<unit>>
                        let! jsOutcome = runNudgeLoop childSession resolvedPromise initialPrompt (fun () -> Terminated)
                        cleanupChild ()
                        return jsOutcome
                with _ ->
                    cleanupChild ()
                    return Terminated
    }
