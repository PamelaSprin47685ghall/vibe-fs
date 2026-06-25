module VibeFs.Omp.ReviewTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.OmpSessionTools
open VibeFs.Kernel.ReviewPrompts
open VibeFs.Kernel.ReviewSession
open VibeFs.Omp.ChildSession
open VibeFs.Omp.Codec
open VibeFs.Omp.MessagingCodec
open VibeFs.Omp.OmpToolSchema
open VibeFs.Omp.Schema
module Dyn = VibeFs.Shell.Dyn
open VibeFs.Shell.ReviewRuntime

let private loopCommand = "loop"
let private maxNudges = 3
let private initialGraceMs = 6000
let private subsequentGraceMs = 10000

type JsReviewResult = { accepted: bool option; feedback: string option; terminated: bool option }

let private terminatedResult fb =
    { accepted = Some false; feedback = Some fb; terminated = Some true }

let private attachReviewChild (store: ReviewStore) (parentId: string) (childId: string) (onResolve: JsReviewResult -> unit) =
    store.addChild(parentId, childId)
    store.setPendingReview(childId, fun kr ->
        let js =
            match kr with
            | Accepted -> { accepted = Some true; feedback = None; terminated = None }
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
            let rec nudgeLoop n =
                promise {
                    if n >= maxNudges then
                        let sm = Dyn.get childSession "sessionManager"
                        let fb = readAssistantText sm 0 "\n\n" |> Option.defaultValue "Reviewer failed to finish."
                        return terminatedResult fb
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
            let! outcome = nudgeLoop 0
            cleanupChild ()
            return outcome
    }

let private handleLoopCommand (pi: obj) (store: ReviewStore) (args: string) (ctx: obj) : JS.Promise<unit> =
    promise {
        let task = args.Trim()
        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            let notify = Dyn.get (Dyn.get ctx "ui") "notify"
            let notifyInfo (msg: string) =
                if Dyn.typeIs notify "function" then
                    emitJsExpr (notify, box msg, box "info")
                        "if (typeof $0 === 'function') $0($1, $2)"
                    |> ignore
            if task = "" then
                store.deactivateReview sessionId
                notifyInfo "loop mode cancelled."
            elif store.isReviewActive sessionId then
                notifyInfo "loop mode is already active."
            else
                store.activateReview(sessionId, task, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                pi?sendMessage(
                    createObj [
                        "customType", box "kunwei-loop-activate"
                        "content",
                            box(
                                String.concat
                                    "\n"
                                    [ $"Task (loop): {task}"
                                      ""
                                      "Loop mode is active."
                                      "Complete the task, then call submit_review with a detailed report and affected files."
                                      "A reviewer will inspect the work and either accept it or return actionable feedback." ])
                        "display", box true
                    ],
                    createObj [ "triggerTurn", box true ])
                notifyInfo "loop mode is active. Finish the task and call submit_review."
    }

let registerLoopFeatures (pi: obj) (store: ReviewStore) : unit =
    let tb = Dyn.get pi "typebox"
    pi?registerCommand(
        loopCommand,
        createObj [
            "description", box "Enable loop review mode for the current session"
            "handler", box(fun (args: string) (ctx: obj) -> handleLoopCommand pi store args ctx |> ignore)
        ])
    pi?registerTool(
        createObj [
            "name", box "submit_review"
            "label", box "Submit Review"
            "description", box "Submit work for review while loop mode is active."
            "parameters",
                objectOf
                    [| ("report", str "Detailed description of what was changed." tb)
                       ("affectedFiles", stringArraySchema pi "Modified or created file path.") |]
                    tb
            "execute",
                box(fun (_id: string) (params': obj) (_s: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        match getSessionIdFromContext ctx with
                        | None -> return errorResult "Loop review is not active for this session."
                        | Some sessionId ->
                            if not (store.isReviewActive sessionId) then
                                return errorResult "Loop review is not active for this session."
                            elif not (store.tryLockReview sessionId) then
                                return errorResult "A review is already in progress."
                            else
                                let report = Dyn.str params' "report"
                                let files =
                                    let a = Dyn.get params' "affectedFiles"
                                    if Dyn.isNullish a || not (Dyn.isArray a) then [||]
                                    else unbox<obj array> a |> Array.map string
                                let mutable loopError : exn option = None
                                let mutable result : JsReviewResult option = None
                                try
                                    let! r = runReviewLoop pi ctx store sessionId report files (store.getReviewTask sessionId)
                                    result <- Some r
                                with ex ->
                                    loopError <- Some ex
                                store.unlockReview sessionId
                                match loopError, result with
                                | Some ex, _ -> return asErrorResult ex
                                | None, None -> return errorResult "Review loop returned no result."
                                | None, Some r ->
                                    if r.feedback.IsNone && not (defaultArg r.terminated false) then
                                        store.deactivateReview sessionId
                                        return textResult "Review passed. Loop mode ended."
                                    elif defaultArg r.terminated false then
                                        store.deactivateReview sessionId
                                        return errorResult ("Review terminated: " + defaultArg r.feedback "")
                                    else
                                        return errorResult ("Review feedback:\n\n" + r.feedback.Value)
                    })
        ])
    pi?registerTool(
        createObj [
            "name", box "return_reviewer"
            "label", box "Return Reviewer"
            "description", box "Submit your review verdict."
            "defaultInactive", box true
            "parameters", returnReviewerParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (_s: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        match getSessionIdFromContext ctx with
                        | None -> return errorResult "No pending review to resolve."
                        | Some sessionId ->
                            match VibeFs.Kernel.ReviewVerdict.parseVerdict (Dyn.str params' "verdict") with
                            | None -> return textResult reviewerNudgePrompt
                            | Some VibeFs.Kernel.ReviewVerdict.Pass ->
                                if not (store.resolvePendingReview(sessionId, Accepted)) then
                                    return errorResult "No pending review to resolve."
                                else
                                    return { textResult "Review submitted: accepted." with display = Some false }
                            | Some VibeFs.Kernel.ReviewVerdict.Reject ->
                                let fb = (Dyn.str params' "feedback").Trim()
                                if fb = "" then return textResult reviewerNudgePrompt
                                elif not (store.resolvePendingReview(sessionId, Rejected fb)) then
                                    return errorResult "No pending review to resolve."
                                else
                                    return { textResult "Review submitted: rejected with feedback." with display = Some false }
                    })
        ])

let registerInputHandler (pi: obj) (store: ReviewStore) : unit =
    pi?on(
        "input",
        box(fun (event: obj) (ctx: obj) ->
            promise {
                let text = (Dyn.str event "text").Trim()
                if not (text.StartsWith("/" + loopCommand)) then
                    return box null
                else
                    let rest = text.Substring(loopCommand.Length + 1).Trim()
                    do! handleLoopCommand pi store rest ctx
                    return createObj [ "handled", box true ]
            }))