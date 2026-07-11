module Wanxiangshu.Omp.ReviewToolsRegister

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.ExecutorTools
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.OmpToolSchema
open Wanxiangshu.Omp.ReviewLoop
open Wanxiangshu.Omp.ReviewToolsLoop
open Wanxiangshu.Omp.Schema
open Wanxiangshu.Shell.DynField
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.ReviewRuntime

module Dyn = Wanxiangshu.Shell.Dyn

let private description (name: string) : string =
    match Wanxiangshu.Kernel.ToolCatalog.description name with
    | Ok d -> d
    | Error e -> failwith e

open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.EventLogRuntime

let private optBool (o: obj) (key: string) : bool option =
    let v = Dyn.get o key
    if Dyn.isNullish v then None else Some(unbox<bool> v)

let executeSubmitReview
    (store: ReviewStore, pi: obj, ctx: obj, id: string, params': obj, signal: obj, onUpdate: obj)
    : JS.Promise<ToolResult> =
    promise {
        let sessionIdOpt =
            if not (Dyn.isNullish ctx) then
                getSessionIdFromContext ctx
            else
                store.getActiveSessionIds () |> List.tryHead

        match sessionIdOpt with
        | None -> return errorResult "Loop review is not active for this session."
        | Some sessionId ->
            let sm = Dyn.get ctx "sessionManager"
            let root = Dyn.str ctx "cwd"
            let activeTask = store.getReviewTask sessionId

            match activeTask with
            | None -> return errorResult "Loop review is not active for this session."
            | Some activeTask ->
                let isWip =
                    let v = Dyn.get params' "wip"
                    if Dyn.isNullish v then true else Dyn.truthy v

                if isWip then
                    do! appendSubmitReviewWipRecorded root sessionId |> Promise.map ignore
                    return textResult (formatWipAcknowledgment activeTask)
                elif not (store.tryLockReview sessionId) then
                    return errorResult "A review is already in progress."
                else
                    let report = Dyn.str params' "report"

                    let files =
                        let a = Dyn.get params' "affectedFiles"

                        if Dyn.isNullish a || not (Dyn.isArray a) then
                            [||]
                        else
                            unbox<obj array> a |> Array.map string

                    let mutable loopError: exn option = None
                    let mutable result: ReviewResult option = None

                    try
                        let! r = runReviewLoop ompScope pi ctx store sessionId report files (Some activeTask)
                        result <- Some r
                    with ex ->
                        loopError <- Some ex

                    store.unlockReview sessionId

                    match loopError, result with
                    | Some ex, _ -> return asErrorResult ex
                    | None, None -> return errorResult "Review loop returned no result."
                    | None, Some r ->
                        let verdict, feedback =
                            match r with
                            | Accepted fb ->
                                Wanxiangshu.Kernel.EventLog.Types.verdictAccepted, (if fb = "" then None else Some fb)
                            | NeedsRevision fb -> Wanxiangshu.Kernel.EventLog.Types.verdictNeedsRevision, Some fb
                            | Terminated -> Wanxiangshu.Kernel.EventLog.Types.verdictTerminated, None

                        do! appendReviewVerdictOrFail root sessionId verdict feedback
                        do! syncReviewFromEventLogDedicated store root sessionId

                        match r with
                        | Accepted fb ->
                            if fb = "" then
                                return textResult "Review passed. Loop mode ended."
                            else
                                return errorResult ("Review feedback:\n\n" + fb)
                        | NeedsRevision fb -> return errorResult ("Review feedback:\n\n" + fb)
                        | Terminated -> return errorResult "Review terminated."
    }

let executeReturnReviewer
    (store: ReviewStore, ctx: obj, id: string, params': obj, signal: obj, onUpdate: obj)
    : JS.Promise<ToolResult> =
    promise {
        let sessionIdOpt =
            match getSessionIdFromContext ctx with
            | Some sid -> Some sid
            | None -> store.getPendingReviewIds () |> List.tryHead

        match sessionIdOpt with
        | None -> return errorResult "No pending review to resolve."
        | Some sessionId ->
            let vStr = Dyn.str params' "verdict"

            match Wanxiangshu.Kernel.ReviewVerdict.parseVerdict vStr with
            | None -> return textResult reviewerNudgePrompt
            | Some Wanxiangshu.Kernel.ReviewVerdict.Perfect ->
                let fb = (Dyn.str params' "feedback").Trim()
                let res = store.resolvePendingReview (sessionId, Accepted fb)

                if not res then
                    return errorResult "No pending review to resolve."
                else
                    return
                        { textResult "Review submitted: accepted." with
                            display = Some false }
            | Some Wanxiangshu.Kernel.ReviewVerdict.Revise ->
                let fb = (Dyn.str params' "feedback").Trim()

                if fb = "" then
                    return textResult reviewerNudgePrompt
                else
                    let res = store.resolvePendingReview (sessionId, NeedsRevision fb)

                    if not res then
                        return errorResult "No pending review to resolve."
                    else
                        return
                            { textResult "Review submitted: revision requested with feedback." with
                                display = Some false }
    }

let registerLoopFeatures (pi: obj) (store: ReviewStore) : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerCommand (
        loopCommand,
        createObj
            [ "description", box "Enable loop review mode for the current session"
              "handler", box (fun (args: string) (ctx: obj) -> handleLoopCommand pi store args ctx) ]
    )

    pi?registerCommand (
        "loop-review",
        createObj
            [ "description",
              box "Pre-check a task before activating loop mode (mirrors Opencode's command.execute.before)."
              "handler", box (fun (args: string) (ctx: obj) -> handleLoopReviewCommand pi store args ctx) ]
    )

    pi?registerTool (
        createObj
            [ "name", box "submit_review"
              "label", box "Submit Review"
              "description", box (description "submit_review")
              "parameters",
              objectOf
                  [| ("report", str "Detailed description of what was changed." tb)
                     ("affectedFiles", stringArraySchema pi "Modified or created file path.")
                     ("wip",
                      opt
                          "Defaults to true: record progress without starting a reviewer. Set to false to start the reviewer for final review."
                          tb
                          bool_) |]
                  tb
              "execute",
              box (
                  System.Func<string, obj, obj, obj, obj, JS.Promise<ToolResult>>(fun id p s u c ->
                      executeSubmitReview (store, pi, c, id, p, s, u))
              ) ]
    )

    pi?registerTool (
        createObj
            [ "name", box "return_reviewer"
              "label", box "Return Reviewer"
              "description", box (description "return_reviewer")
              "defaultInactive", box true
              "parameters", returnReviewerParameters tb
              "execute",
              box (
                  System.Func<string, obj, obj, obj, obj, JS.Promise<ToolResult>>(fun id p s u c ->
                      executeReturnReviewer (store, c, id, p, s, u))
              ) ]
    )

let registerInputHandler (pi: obj) (store: ReviewStore) : unit =
    pi?on (
        "input",
        box (fun (event: obj) (ctx: obj) ->
            promise {
                let text = (Dyn.str event "text").Trim()

                if not (text.StartsWith("/" + loopCommand)) then
                    return box null
                else
                    let rest = text.Substring(loopCommand.Length + 1).Trim()
                    do! handleLoopCommand pi store rest ctx
                    return createObj [ "handled", box true ]
            })
    )
