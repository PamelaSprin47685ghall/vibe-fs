module VibeFs.Mux.SlashCommands

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Config
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.ReviewSession
open VibeFs.Kernel.Domain
open VibeFs.Shell.ReviewRuntime
open VibeFs.Shell.CallStore
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Mux.SubagentTools

let private dateNow () : int64 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

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

let createLoopOnlyCommand (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
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
                        Promise.lift "With-Review Mode cancelled."
                    elif reviewStore.isReviewActive (Id.workspaceIdValue wid) then
                        Promise.lift "With-Review Mode is already active. Submit your work via submit_review."
                    else
                        reviewStore.activateReview(Id.workspaceIdValue wid, task, dateNow ())
                        Promise.lift (buildLoopMessage task [ "With-Review Mode is active. Complete the task above, then call submit_review with:" ])) |}

let private parseLoopReviewVerdict (args: obj option) (report: string) : bool * string =
    match args with
    | Some a ->
        let v = defaultArg (strField a "verdict") "" |> fun s -> s.Trim().ToLowerInvariant()
        let feedback = defaultArg (strField a "feedback") ""
        if v = "pass" then true, ""
        elif v = "reject" then false, feedback
        else false, report
    | None -> false, report

let private submissionFooter (toolName: string) (callId: string) =
    "\n\nYou must call the `" + toolName + "` tool to submit your answer. "
    + "Use callId `" + callId + "`. Do not write files, run commands, or modify the workspace."

let private precheckReview
    (deps: obj) (toolNames: string array) (callStore: CallStore) (workspaceId: WorkspaceId) (task: string)
    : JS.Promise<DelegateOutcome * obj option> =
    promise {
        let! config = pluginConfigForSlash deps workspaceId
        let disabledTools = deniedTools "reviewer" (Array.toList toolNames) |> Array.ofList
        let workspaceIdStr = Id.workspaceIdValue workspaceId
        let callId = workspaceIdStr + "-loop-review-" + string (dateNow ())
        let verdictPromise = registerCallWithTimeout callStore callId 300000
        let experiments =
            createObj
                [ "subagentRole", box "reviewer"
                  "toolPolicy", box (createObj [ "disabledTools", box disabledTools ]) ]
        let opts = createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]
        let promptText = ReviewerVerdictPrompts.loopReviewVerdictInstructions + "\n\n=== Task Description ===\n\n" + task + "\n\n" + submissionFooter "agent_report" callId
        let! outcome = delegateWithTimeout deps config "explore" promptText "Pre-review" (Some opts) 300000
        let! verdictArgs =
            promise {
                try
                    let! args = verdictPromise
                    return Some args
                with _ -> return None
            }
        return outcome, verdictArgs
    }

let private activateReview
    (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (workspaceIdStr: string) (task: string)
    (isPass: bool) (feedback: string) : string =
    reviewStore.activateReview(workspaceIdStr, task, dateNow ())
    if isPass then
        buildLoopMessage task [ "With-Review Mode is active. Pre-review passed. Complete the task above, then call submit_review with:" ]
    else
        buildLoopMessage task [ "Pre-review feedback:"; ""; feedback; ""; "With-Review Mode is active. Address the pre-review feedback above while completing the task. Then call submit_review with:" ]

let private loopReviewExecute
    (deps: obj) (toolNames: string array) (callStore: CallStore) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (workspaceId: WorkspaceId) (args: string) : JS.Promise<string> =
    let task = args.Trim()
    let workspaceIdStr = Id.workspaceIdValue workspaceId
    if task = "" then
        reviewStore.deactivateReview workspaceIdStr
        Promise.lift "With-Review Mode cancelled."
    elif reviewStore.isReviewActive workspaceIdStr then
        Promise.lift "With-Review Mode is already active. Submit your work via submit_review."
    else
        promise {
            let! outcome, verdictArgs = precheckReview deps toolNames callStore workspaceId task
            let reportText =
                match outcome with
                | Report r -> r
                | TimedOut -> "Pre-review timed out."
            let isPass, feedback = parseLoopReviewVerdict verdictArgs reportText
            match outcome with
            | TimedOut ->
                return buildLoopMessage task [ "With-Review Mode was NOT activated because the pre-review timed out. Please retry /loop-review." ]
            | Report _ ->
                return activateReview reviewStore workspaceIdStr task isPass feedback
        }

let createLoopReviewCommand (deps: obj) (toolNames: string array) (callStore: CallStore) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    box
        {| key = "loop-review"
           description = "Pre-review task description with a reviewer sub-agent, then activate With-Review Mode."
           inputHint = "<task description>"
           execute =
               System.Func<string, string, JS.Promise<string>>(fun workspaceIdStr args ->
                   match Id.tryWorkspaceId workspaceIdStr with
                   | None -> Promise.lift "Invalid workspaceId"
                   | Some wid -> loopReviewExecute deps toolNames callStore reviewStore wid args) |}

let createSlashCommands (deps: obj) (toolNames: string array) (callStore: CallStore) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj array =
    [| createLoopOnlyCommand reviewStore; createLoopReviewCommand deps toolNames callStore reviewStore |]
