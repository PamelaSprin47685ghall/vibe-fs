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

let private waitUntilResolved (resolved: JsReviewResult option ref) : JS.Promise<JsReviewResult> =
    let rec loop () =
        promise {
            if resolved.Value.IsSome then return resolved.Value.Value
            else
                do! Promise.sleep 50
                return! loop ()
        }
    loop ()

let private raceResolvedOr (resolved: JsReviewResult option ref) (other: JS.Promise<unit>) : JS.Promise<JsReviewResult option> =
    promise {
        let! winner =
            Promise.race [
                waitUntilResolved resolved |> Promise.map Some
                other |> Promise.map (fun () -> None)
            ]
        return winner
    }

let private maxNudges = 3
let private initialGraceMs = 6000
let private subsequentGraceMs = 10000

let private runNudgeLoop (childSession: obj) (resolved: JsReviewResult option ref) (onMaxNudges: unit -> JsReviewResult)
    : JS.Promise<JsReviewResult> =
    let rec nudgeLoop n =
        promise {
            if n >= maxNudges then return onMaxNudges ()
            else
                let! first = raceResolvedOr resolved (childSession?waitForIdle() |> unbox<JS.Promise<unit>>)
                match first with
                | Some r -> return r
                | None ->
                    let grace = if n = 0 then initialGraceMs else subsequentGraceMs
                    let! second = raceResolvedOr resolved (Promise.sleep grace)
                    match second with
                    | Some r -> return r
                    | None ->
                        do! childSession?prompt(reviewerNudgePrompt) |> unbox<JS.Promise<unit>>
                        return! nudgeLoop (n + 1)
        }
    nudgeLoop 0

let runReviewLoop (pi: obj) (ctx: obj) (store: ReviewStore) (parentId: string) (report: string) (files: string array) (task: string option)
    : JS.Promise<JsReviewResult> =
    promise {
        let resolved = ref None
        let! child = createChildSession pi ctx ompReviewChildToolNames None [||]
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
            attachReviewChild store parentId childId (fun r -> resolved.Value <- Some r)
            let initial = buildOmpReviewInitialPrompt report (files |> Array.toList) task
            do! childSession?prompt(initial) |> unbox<JS.Promise<unit>>
            let onMax () =
                let sm = Dyn.get childSession "sessionManager"
                let fb = readAssistantText sm 0 "\n\n" |> Option.defaultValue "Reviewer failed to finish."
                terminatedResult fb
            let! outcome = runNudgeLoop childSession resolved onMax
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
                let resolved = ref None
                let! child = createChildSession pi ctx ompReviewChildToolNames None [||]
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
                    attachReviewChild store parentId childId (fun r -> resolved.Value <- Some r)
                    let initial = reviewerPrompt taskTrim "" []
                    do! childSession?prompt(initial) |> unbox<JS.Promise<unit>>
                    let! jsOutcome = runNudgeLoop childSession resolved (fun () -> terminatedResult "Pre-review timed out.")
                    cleanupChild ()
                    return jsToReviewResult jsOutcome
    }