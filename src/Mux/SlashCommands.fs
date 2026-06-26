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
open Wanxiangshu.Shell.Dyn

[<Global("process")>]
let private nodeProcess : obj = jsNative

let private fallbackSlashConfig (deps: obj) (workspaceId: WorkspaceId) : obj =
    createObj
        [ "cwd", box (nodeProcess?cwd())
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

let createLoopOnlyCommand (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) : obj =
    box {| key = "loop"
           description = "Activate With-Review Mode. AI completes task, submits for review."
           inputHint = "<task description>"
           execute = System.Func<string, string, JS.Promise<string>>(fun workspaceIdStr args ->
                match Id.tryWorkspaceId workspaceIdStr with
                | None -> Promise.lift "Invalid workspaceId"
                | Some wid ->
                    let task = args.Trim()
                    if task = "" then
                        reviewStore.deactivateReview (Id.workspaceIdValue wid)
                        Promise.lift loopCancelledMessage
                    elif reviewStore.isReviewActive (Id.workspaceIdValue wid) then
                        Promise.lift "With-Review Mode is already active. Submit your work via submit_review."
                    else
                        reviewStore.activateReview(Id.workspaceIdValue wid, task, Domain.nowMs ())
                        Promise.lift (buildLoopMessage task [ "With-Review Mode is active. Complete the task above, then call submit_review with:" ])) |}

let private precheckReview
    (deps: obj) (toolNames: string array) (workspaceId: WorkspaceId) (task: string)
    : JS.Promise<DelegateOutcome> =
    promise {
        let! config = pluginConfigForSlash deps workspaceId
        let disabledTools = deniedTools "reviewer" (Array.toList toolNames) |> Array.ofList
        let experiments =
            createObj
                [ "subagentRole", box "reviewer"
                  "toolPolicy", box (createObj [ "disabledTools", box disabledTools ]) ]
        let opts = createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]
        let promptText = preReviewVerdictPrompt task
        return! Delegate.delegateWithTimeout deps config "explore" promptText "Pre-review" (Some opts) 300000
    }

let private activateReview
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) (workspaceIdStr: string) (task: string)
    (isPass: bool) (feedback: string) : string =
    reviewStore.activateReview(workspaceIdStr, task, Domain.nowMs ())
    if isPass then
        buildLoopMessage task [ "With-Review Mode is active. Pre-review passed. Complete the task above, then call submit_review with:" ]
    else
        buildLoopMessage task [ "Pre-review feedback:"; ""; feedback; ""; "With-Review Mode is active. Address the pre-review feedback above while completing the task. Then call submit_review with:" ]

let private loopReviewExecute
    (deps: obj) (toolNames: string array) (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (workspaceId: WorkspaceId) (args: string) : JS.Promise<string> =
    let task = args.Trim()
    let workspaceIdStr = Id.workspaceIdValue workspaceId
    if task = "" then
        reviewStore.deactivateReview workspaceIdStr
        Promise.lift loopCancelledMessage
    elif reviewStore.isReviewActive workspaceIdStr then
        Promise.lift "With-Review Mode is already active. Submit your work via submit_review."
    else
        promise {
            let! outcome = precheckReview deps toolNames workspaceId task
            match outcome with
            | DelegateTimeout.TimedOut ->
                return buildLoopMessage task [ "With-Review Mode was NOT activated because the pre-review timed out. Please retry /loop-review." ]
            | DelegateTimeout.Report markdown ->
                let isPass, feedback =
                    match parseReviewReportMarkdown markdown with
                    | Accepted -> true, ""
                    | Rejected fb -> false, fb
                    | Terminated -> false, markdown
                return activateReview reviewStore workspaceIdStr task isPass feedback
        }

let createLoopReviewCommand (deps: obj) (toolNames: string array) (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) : obj =
    box
        {| key = "loop-review"
           description = "Pre-review task description with a reviewer sub-agent, then activate With-Review Mode."
           inputHint = "<task description>"
           execute =
               System.Func<string, string, JS.Promise<string>>(fun workspaceIdStr args ->
                   match Id.tryWorkspaceId workspaceIdStr with
                   | None -> Promise.lift "Invalid workspaceId"
                   | Some wid -> loopReviewExecute deps toolNames reviewStore wid args) |}

let createSlashCommands (deps: obj) (toolNames: string array) (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) : obj array =
    [| createLoopOnlyCommand reviewStore; createLoopReviewCommand deps toolNames reviewStore |]
