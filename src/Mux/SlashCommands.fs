module Wanxiangshu.Mux.SlashCommands

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewVerdict
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Mux.Delegate
open Wanxiangshu.Mux.DelegateTimeout
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Mux.SubagentTools
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Clock
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.RuntimeScope

[<Global("process")>]
let private nodeProcess: obj = jsNative

let private eventLogRootFromDeps (deps: obj) : string =
    if Dyn.isNullish deps then
        unbox<string> (nodeProcess?cwd ())
    else
        let d = Dyn.str deps "directory"
        if d <> "" then d else unbox<string> (nodeProcess?cwd ())

let private syncReviewTaskFromHistory
    (scope: RuntimeScope)
    (deps: obj)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (sessionID: string)
    : JS.Promise<string option> =
    promise {
        let root = eventLogRootFromDeps deps
        scope.TriggerInit(root)
        do! scope.WaitInit()

        let getHistory =
            if Dyn.isNullish deps then
                null
            else
                Dyn.get deps "getChatHistory"

        if sessionID = "" || Dyn.isNullish getHistory then
            return reviewStore.getReviewTask sessionID
        else
            try
                let! _history = unbox<JS.Promise<obj array>> (getHistory $ sessionID)
                return reviewStore.getReviewTask sessionID
            with _ ->
                return reviewStore.getReviewTask sessionID
    }

let private fallbackSlashConfig (deps: obj) (workspaceId: WorkspaceId) : obj =
    createObj
        [ "cwd", box (nodeProcess?cwd ())
          "workspaceId", box (Id.workspaceIdValue workspaceId)
          "taskService", box (Dyn.get deps "taskService") ]

let private slashConfigFromCtx (deps: obj) (workspaceId: WorkspaceId) (ctx: obj) : obj =
    if Dyn.isNullish ctx then
        fallbackSlashConfig deps workspaceId
    else
        let runtimeObj = Dyn.get ctx "runtime"
        let runtime = if Dyn.isNullish runtimeObj then null else runtimeObj

        createObj
            [ "cwd", box (Dyn.str ctx "cwd")
              "workspaceId", box (Id.workspaceIdValue workspaceId)
              "runtime", box runtime
              "muxEnv", box (Dyn.get ctx "muxEnv")
              "taskService", box (Dyn.get deps "taskService") ]

let private pluginConfigForSlash (deps: obj) (workspaceId: WorkspaceId) : JS.Promise<obj> =
    promise {
        let resolver = Dyn.get deps "resolveWorkspacePluginContext"

        if not (Dyn.typeIs resolver "function") then
            return fallbackSlashConfig deps workspaceId
        else
            let! ctx = unbox<JS.Promise<obj>> (Dyn.call2 resolver (box (Id.workspaceIdValue workspaceId)) (box null))
            return slashConfigFromCtx deps workspaceId ctx
    }

let createLoopOnlyCommand
    (deps: obj)
    (scope: RuntimeScope)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    : obj =
    box
        {| key = "loop"
           description = "Activate With-Review Mode. AI completes task, submits for review."
           inputHint = "<task description>"
           execute =
            System.Func<string, string, JS.Promise<string>>(fun workspaceIdStr args ->
                match Id.tryWorkspaceId workspaceIdStr with
                | None -> Promise.lift "Invalid workspaceId"
                | Some wid ->
                    promise {
                        let root = eventLogRootFromDeps deps
                        scope.TriggerInit(root)
                        do! scope.WaitInit()
                        let sid = Id.workspaceIdValue wid
                        let task = args.Trim()
                        let existingTask = reviewStore.getReviewTask sid

                        if task = "" then
                            do! appendLoopCancelledOrFail root sid
                            do! syncReviewFromEventLogDedicated reviewStore root sid
                            return loopCancelledMessage
                        elif existingTask.IsSome then
                            return "With-Review Mode is already active. Submit your work via submit_review."
                        else
                            do! appendLoopActivatedOrFail root sid task
                            do! syncReviewFromEventLogDedicated reviewStore root sid

                            return
                                buildLoopMessage
                                    task
                                    [ "With-Review Mode is active. Complete the task above, then call submit_review with:" ]
                    }) |}

let private precheckReview
    (deps: obj)
    (toolNames: string array)
    (workspaceId: WorkspaceId)
    (task: string)
    : JS.Promise<DelegateOutcome> =
    promise {
        let! config = pluginConfigForSlash deps workspaceId
        let disabledTools = deniedTools "reviewer" (Array.toList toolNames) |> Array.ofList

        let experiments =
            createObj
                [ "subagentRole", box "reviewer"
                  "toolPolicy", box (createObj [ "disabledTools", box disabledTools ]) ]

        let opts =
            createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]

        let promptText = preReviewVerdictPrompt task
        return! Delegate.delegateWithTimeout deps config "explore" promptText "Pre-review" (Some opts) 300000
    }

let private finalizeLoopReviewActivation
    (deps: obj)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (workspaceIdStr: string)
    (task: string)
    (reviewResult: ReviewResult)
    : JS.Promise<string> =
    promise {
        let root = eventLogRootFromDeps deps
        do! appendLoopActivatedOrFail root workspaceIdStr task
        do! syncReviewFromEventLogDedicated reviewStore root workspaceIdStr

        match reviewResult with
        | Accepted _ ->
            return
                buildLoopMessage
                    task
                    [ "With-Review Mode is active. Pre-review passed. Complete the task above, then call submit_review with:" ]
        | NeedsRevision fb ->
            return
                buildLoopMessage
                    task
                    [ "Pre-review feedback:"
                      ""
                      fb
                      ""
                      "With-Review Mode is active. Address the pre-review feedback above while completing the task. Then call submit_review with:" ]
        | Terminated ->
            return
                buildLoopMessage
                    task
                    [ "With-Review Mode is active. Pre-review was terminated. Complete the task above, then call submit_review with:" ]
    }

let private loopReviewExecute
    (scope: RuntimeScope)
    (deps: obj)
    (toolNames: string array)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (workspaceId: WorkspaceId)
    (args: string)
    : JS.Promise<string> =
    let task = args.Trim()
    let workspaceIdStr = Id.workspaceIdValue workspaceId

    if task = "" then
        promise {
            let root = eventLogRootFromDeps deps
            scope.TriggerInit(root)
            do! scope.WaitInit()
            do! appendLoopCancelledOrFail root workspaceIdStr
            do! syncReviewFromEventLogDedicated reviewStore root workspaceIdStr
            return loopCancelledMessage
        }
    else
        promise {
            let root = eventLogRootFromDeps deps
            scope.TriggerInit(root)
            do! scope.WaitInit()
            let! existingTask = syncReviewTaskFromHistory scope deps reviewStore workspaceIdStr

            if existingTask.IsSome then
                return "With-Review Mode is already active. Submit your work via submit_review."
            else
                let! outcome = precheckReview deps toolNames workspaceId task

                match outcome with
                | DelegateTimeout.TimedOut ->
                    return
                        buildLoopMessage
                            task
                            [ "With-Review Mode was NOT activated because the pre-review timed out. Please retry /loop-review." ]
                | DelegateTimeout.Report markdown ->
                    let reviewResult = parseReviewReportMarkdown markdown
                    return! finalizeLoopReviewActivation deps reviewStore workspaceIdStr task reviewResult
        }

let createLoopReviewCommand
    (scope: RuntimeScope)
    (deps: obj)
    (toolNames: string array)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    : obj =
    box
        {| key = "loop-review"
           description = "Pre-review task description with a reviewer sub-agent, then activate With-Review Mode."
           inputHint = "<task description>"
           execute =
            System.Func<string, string, JS.Promise<string>>(fun workspaceIdStr args ->
                match Id.tryWorkspaceId workspaceIdStr with
                | None -> Promise.lift "Invalid workspaceId"
                | Some wid -> loopReviewExecute scope deps toolNames reviewStore wid args) |}

let createSlashCommands
    (scope: RuntimeScope)
    (deps: obj)
    (toolNames: string array)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    : obj array =
    [| createLoopOnlyCommand deps scope reviewStore
       createLoopReviewCommand scope deps toolNames reviewStore |]
