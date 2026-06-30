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
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ReviewRuntime

type JsReviewResult = { accepted: bool option; feedback: string option; terminated: bool option }

let private terminatedResult fb =
    { accepted = Some false; feedback = Some fb; terminated = Some true }

let private createDeferred () : JS.Promise<JsReviewResult> * (JsReviewResult -> unit) =
    let d = emitJsExpr () "Promise.withResolvers()"
    unbox (Dyn.get d "promise"), unbox (Dyn.get d "resolve")

let private attachReviewChild (store: ReviewStore) (parentId: string) (childId: string) (onResolve: JsReviewResult -> unit) =
    store.addChild(parentId, childId)
    store.setPendingReview(childId, fun kr ->
        let js =
            match kr with
            | Accepted fb -> { accepted = Some true; feedback = (if fb = "" then None else Some fb); terminated = None }
            | Rejected fb -> { accepted = Some false; feedback = Some fb; terminated = None }
            | Terminated -> terminatedResult "Review session closed."
        onResolve js)

let private detachReviewChild (store: ReviewStore) (_parentId: string) (childId: string) =
    store.resolvePendingReview(childId, Terminated) |> ignore
    store.unlockReview childId

let private maxNudges = 3
let mutable initialGraceMs = 6000
let mutable subsequentGraceMs = 10000

let resetReviewLoopGracePeriods () =
    initialGraceMs <- 6000
    subsequentGraceMs <- 10000

let private scheduleTimeout (fn: unit -> unit) (ms: int) : int =
    emitJsExpr (fn, ms) "setTimeout($0, $1)"

let private cancelScheduledTimeout (id: int) : unit =
    emitJsExpr (id) "clearTimeout($0)"

let private runNudgeLoop (childSession: obj) (resolvedPromise: JS.Promise<JsReviewResult>) (onMaxNudges: unit -> JsReviewResult)
    : JS.Promise<JsReviewResult> =
    promise {
        let deferred, resolveDeferred = createDeferred ()
        let cancelled = ref false
        let timeoutHandle = ref 0

        // When resolvedPromise resolves, cancel pending timeout and resolve deferred
        resolvedPromise |> Promise.map (fun r ->
            if not cancelled.Value then
                cancelled.Value <- true
                cancelScheduledTimeout timeoutHandle.Value
                resolveDeferred r
            r
        ) |> ignore

        let rec scheduleNext n =
            if n >= maxNudges then
                if not cancelled.Value then
                    cancelled.Value <- true
                    resolveDeferred (onMaxNudges ())
            else
                let grace = if n = 0 then initialGraceMs else subsequentGraceMs
                timeoutHandle.Value <-
                    scheduleTimeout (fun () ->
                        if not cancelled.Value then
                            childSession?prompt(reviewerNudgePrompt) |> unbox<JS.Promise<unit>>
                            |> Promise.map (fun () ->
                                if not cancelled.Value then
                                    scheduleNext (n + 1)
                            ) |> ignore
                    ) grace

        scheduleNext 0
        return! deferred
    }

let runReviewLoop (pi: obj) (ctx: obj) (store: ReviewStore) (parentId: string) (report: string) (files: string array) (task: string option)
    : JS.Promise<JsReviewResult> =
    promise {
        let resolvedPromise, resolveReview = createDeferred ()
        let! child = createChildSession pi ctx ompReviewChildToolNames None [||] None
        let childSession = child.session
        let childCtx = createObj [ "sessionManager", Dyn.get childSession "sessionManager" ]
        let childId = getSessionIdFromContext childCtx |> Option.defaultValue ""
        let cleanupChild () =
            detachReviewChild store parentId childId
            let abort = Dyn.get childSession "abort"
            if Dyn.typeIs abort "function" then Dyn.call0 abort |> ignore
            child.dispose |> Option.iter (fun dispose -> dispose ())
        if childId = "" then
            let abort = Dyn.get childSession "abort"
            if Dyn.typeIs abort "function" then Dyn.call0 abort |> ignore
            child.dispose |> Option.iter (fun dispose -> dispose ())
            return terminatedResult "Review child session unavailable"
        else
            attachReviewChild store parentId childId (fun r -> resolveReview r)
            let initial = buildOmpReviewInitialPrompt report (files |> Array.toList) task
            do! childSession?prompt(initial) |> unbox<JS.Promise<unit>>
            let onMax () =
                let sm = Dyn.get childSession "sessionManager"
                let fb = readAssistantText sm 0 "\n\n" |> Option.defaultValue "Reviewer failed to finish."
                terminatedResult fb
            let! outcome = runNudgeLoop childSession resolvedPromise onMax
            cleanupChild ()
            return outcome
    }

let private jsToReviewResult (js: JsReviewResult) : ReviewResult =
    if defaultArg js.terminated false then Terminated
    elif js.accepted = Some true then Accepted(defaultArg js.feedback "")
    elif js.accepted = Some false then Rejected(defaultArg js.feedback "")
    else Terminated

let runPreReviewerSession (pi: obj) (ctx: obj) (store: ReviewStore) (task: string) : JS.Promise<ReviewResult> =
    promise {
        let taskTrim = task.Trim()
        if taskTrim = "" then return Terminated
        else
            match getSessionIdFromContext ctx with
            | None -> return Terminated
            | Some parentId ->
                let resolvedPromise, resolveReview = createDeferred ()
                let! child = createChildSession pi ctx ompReviewChildToolNames None [||] None
                let childSession = child.session
                let childCtx = createObj [ "sessionManager", Dyn.get childSession "sessionManager" ]
                let childId = getSessionIdFromContext childCtx |> Option.defaultValue ""
                let cleanupChild () =
                    detachReviewChild store parentId childId
                    let abort = Dyn.get childSession "abort"
                    if Dyn.typeIs abort "function" then Dyn.call0 abort |> ignore
                    child.dispose |> Option.iter (fun dispose -> dispose ())
                if childId = "" then
                    cleanupChild ()
                    return Terminated
                else
                    attachReviewChild store parentId childId (fun r -> resolveReview r)
                    let initial = reviewerPrompt taskTrim "" []
                    do! childSession?prompt(initial) |> unbox<JS.Promise<unit>>
                    let! jsOutcome = runNudgeLoop childSession resolvedPromise (fun () -> terminatedResult "Pre-review timed out.")
                    cleanupChild ()
                    return jsToReviewResult jsOutcome
    }