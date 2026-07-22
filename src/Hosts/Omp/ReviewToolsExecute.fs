module Wanxiangshu.Hosts.Omp.ReviewToolsExecute

open Fable.Core
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.OmpToolSchema
open Wanxiangshu.Hosts.Omp.ReviewLoop
open Wanxiangshu.Hosts.Omp.ReviewToolsLoop
open Wanxiangshu.Runtime.DynField
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ReviewEventWriter
open Wanxiangshu.Runtime.ReviewRuntime

module Dyn = Wanxiangshu.Runtime.Dyn

let private tryGetSessionId (ctx: obj) (store: ReviewStore) : string option =
    if not (Dyn.isNullish ctx) then
        getSessionIdFromContext ctx
    else
        store.getActiveSessionIds () |> List.tryHead

let private isWorkInProgress (params': obj) : bool =
    let v = Dyn.get params' "wip"
    if Dyn.isNullish v then true else Dyn.truthy v

let private parseAffectedFiles (params': obj) : string array =
    let a = Dyn.get params' "affectedFiles"

    if Dyn.isNullish a || not (Dyn.isArray a) then
        [||]
    else
        unbox<obj array> a |> Array.map string

let private handleReviewResult
    (root: string)
    (store: ReviewStore)
    (sessionId: string)
    (r: ReviewResult)
    : JS.Promise<ToolResult> =
    promise {
        let verdict, feedback =
            match r with
            | Accepted fb ->
                Wanxiangshu.Kernel.EventSourcing.EventKind.verdictAccepted, (if fb = "" then None else Some fb)
            | NeedsRevision fb -> Wanxiangshu.Kernel.EventSourcing.EventKind.verdictNeedsRevision, Some fb
            | Terminated -> Wanxiangshu.Kernel.EventSourcing.EventKind.verdictTerminated, None

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

let executeSubmitReview
    (store: ReviewStore, pi: obj, ctx: obj, id: string, params': obj, signal: obj, onUpdate: obj)
    : JS.Promise<ToolResult> =
    promise {
        let sessionIdOpt = tryGetSessionId ctx store

        match sessionIdOpt with
        | None -> return errorResult "Loop review is not active for this session."
        | Some sessionId ->
            let sm = Dyn.get ctx "sessionManager"
            let root = Dyn.str ctx "cwd"
            let activeTask = store.getReviewTask sessionId

            match activeTask with
            | None -> return errorResult "Loop review is not active for this session."
            | Some activeTask ->
                if isWorkInProgress params' then
                    let report = Dyn.str params' "report"
                    do! appendSubmitReviewWipRecorded root sessionId report |> Promise.map ignore
                    return textResult (formatWipAcknowledgment activeTask)
                elif not (store.tryLockReview sessionId) then
                    return errorResult "A review is already in progress."
                else
                    let report = Dyn.str params' "report"
                    let files = parseAffectedFiles params'
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
                    | None, Some r -> return! handleReviewResult root store sessionId r
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
            | Some verdict ->
                let fb = (Dyn.str params' "feedback").Trim()
                let doubleCheckDone = store.isChallengeRequested sessionId

                match Wanxiangshu.Kernel.ReviewVerdict.decideReviewSubmission verdict fb doubleCheckDone with
                | Wanxiangshu.Kernel.ReviewVerdict.AskDoubleCheck ->
                    store.recordChallengeRequested sessionId
                    let task = store.getReviewTask sessionId |> Option.defaultValue ""
                    return textResult (doubleCheckPrompt task)
                | Wanxiangshu.Kernel.ReviewVerdict.Finalize result ->
                    let res = store.resolvePendingReview (sessionId, result)

                    if not res then
                        return errorResult "No pending review to resolve."
                    else
                        match result with
                        | Accepted _ ->
                            return
                                { textResult "Review submitted: accepted." with
                                    display = Some false }
                        | NeedsRevision _ ->
                            return
                                { textResult "Review submitted: revision requested with feedback." with
                                    display = Some false }
                        | Terminated -> return errorResult "Review terminated."
    }
