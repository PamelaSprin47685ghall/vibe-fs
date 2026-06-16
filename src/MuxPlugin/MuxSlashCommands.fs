module VibeFs.MuxPlugin.MuxSlashCommands

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.ToolPolicy
open VibeFs.Kernel.HostKernel
open VibeFs.Kernel.ReviewSession
open VibeFs.Shell.ReviewRuntime
open VibeFs.MuxPlugin.Delegate
open VibeFs.MuxPlugin.CallStore
open VibeFs.MuxPlugin.MuxTools.Shared

let private dateNow () : int64 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

[<Global("process")>]
let private nodeProcess : obj = jsNative

let private fallbackSlashConfig (deps: obj) (workspaceId: string) : obj =
    createObj
        [ "cwd", box (nodeProcess?cwd())
          "workspaceId", box workspaceId
          "taskService", box (Dyn.get deps "taskService") ]

let private slashConfigFromCtx (deps: obj) (workspaceId: string) (ctx: obj) : obj =
    if Dyn.isNullish ctx then
        fallbackSlashConfig deps workspaceId
    else
        let runtimeObj = Dyn.get ctx "runtime"
        let runtime = if Dyn.isNullish runtimeObj then null else runtimeObj
        createObj
            [ "cwd", box (Dyn.str ctx "cwd")
              "workspaceId", box workspaceId
              "runtime", box runtime
              "muxEnv", box (Dyn.get ctx "muxEnv")
              "taskService", box (Dyn.get deps "taskService") ]

let private pluginConfigForSlash (deps: obj) (workspaceId: string) : JS.Promise<obj> =
    async {
        let resolver = Dyn.get deps "resolveWorkspacePluginContext"
        if not (Dyn.typeIs resolver "function") then
            return fallbackSlashConfig deps workspaceId
        else
            let! ctx = Dyn.call2 resolver (box workspaceId) (box null) :?> JS.Promise<obj> |> Async.AwaitPromise
            return slashConfigFromCtx deps workspaceId ctx
    } |> Async.StartAsPromise

let private loopFooter =
    [ "- report: a detailed description of what you did and why"
      "- affectedFiles: list of every file you modified or created"
      ""
      "A reviewer will examine your submission. If accepted, you are done. If rejected, you will receive specific feedback to address." ]

let private buildLoopMessage (task: string) (bodyLines: string list) : string =
    let header = [ "Task (loop): " + task; "" ]
    (header @ bodyLines @ loopFooter) |> String.concat "\n"

let createLoopOnlyCommand (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    box {| key = "loop"
           description = "Activate review loop mode. AI completes task, submits for review."
           inputHint = "<task description>"
           execute = System.Func<string, string, JS.Promise<string>>(fun workspaceId args ->
                let task = args.Trim()
                if task = "" then
                    reviewStore.deactivateReview workspaceId
                    (async { return "Loop mode cancelled." } |> Async.StartAsPromise)
                elif reviewStore.isReviewActive workspaceId then
                    (async { return "Loop mode is already active. Submit your work via submit_review." } |> Async.StartAsPromise)
                else
                    reviewStore.activateReview(workspaceId, task, dateNow ())
                    (async { return buildLoopMessage task [ "Loop mode is active. Complete the task above, then call submit_review with:" ] } |> Async.StartAsPromise)) |}

let private loopReviewVerdictInstructions =
    "You are a reviewer evaluating whether a task description is clear and actionable enough to begin work.\n\n"
    + "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
    + "- verdict: \"PASS\" if the task is clear, specific, and actionable, \"REJECT\" otherwise\n"
    + "- feedback: detailed, actionable feedback when rejecting; empty string when passing\n"
    + "- callId: the callId supplied in this prompt\n\n"
    + "Do not output free-form text as your final answer; the tool call is required."

let private parseLoopReviewVerdict (args: obj option) (report: string) : bool * string =
    match args with
    | Some a ->
        let v = defaultArg (VibeFs.MuxPlugin.MuxTools.Shared.strField a "verdict") "" |> fun s -> s.Trim().ToLowerInvariant()
        let feedback = defaultArg (VibeFs.MuxPlugin.MuxTools.Shared.strField a "feedback") ""
        if v = "pass" then true, ""
        elif v = "reject" then false, feedback
        else false, report
    | None -> false, report

let private submissionFooter (toolName: string) (callId: string) =
    "\n\nYou must call the `" + toolName + "` tool to submit your answer. "
    + "Use callId `" + callId + "`. Do not write files, run commands, or modify the workspace."

let private loopReviewExecute
    (deps: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (workspaceId: string) (args: string) : JS.Promise<string> =
    let task = args.Trim()
    if task = "" then
        reviewStore.deactivateReview workspaceId
        (async { return "Loop mode cancelled." } |> Async.StartAsPromise)
    elif reviewStore.isReviewActive workspaceId then
        (async { return "Loop mode is already active. Submit your work via submit_review." } |> Async.StartAsPromise)
    else
        async {
            let! config = pluginConfigForSlash deps workspaceId |> Async.AwaitPromise
            let disabledTools = deniedTools "reviewer" (Array.toList registeredToolNames) |> Array.ofList
            let callId = workspaceId + "-loop-review-" + string (dateNow ())
            let verdictPromise = registerCallWithTimeout callId 300000
            let experiments =
                createObj
                    [ "subagentRole", box "reviewer"
                      "toolPolicy", box (createObj [ "disabledTools", box disabledTools ]) ]
            let opts = createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]
            let promptText = loopReviewVerdictInstructions + "\n\n=== Task Description ===\n\n" + task + "\n\n" + submissionFooter "agent_report" callId
            let! outcome = delegateWithTimeout deps config "explore" promptText "Pre-review" (Some opts) 300000
            let! verdictArgs =
                async {
                    try
                        let! args = verdictPromise |> Async.AwaitPromise
                        return Some args
                    with _ -> return None
                }
            let reportText =
                match outcome with
                | Report r -> r
                | TimedOut -> "Pre-review timed out."
            let isPass, feedback = parseLoopReviewVerdict verdictArgs reportText
            match outcome with
            | TimedOut ->
                return buildLoopMessage task [ "Loop mode was NOT activated because the pre-review timed out. Please retry /loop-review." ]
            | Report _ ->
                reviewStore.activateReview(workspaceId, task, dateNow ())
                return
                    if isPass then
                        buildLoopMessage task [ "Loop mode is active. Pre-review passed. Complete the task above, then call submit_review with:" ]
                    else
                        buildLoopMessage task [ "Pre-review feedback:"; ""; feedback; ""; "Loop mode is active. Address the pre-review feedback above while completing the task. Then call submit_review with:" ]
        } |> Async.StartAsPromise

let createLoopReviewCommand (deps: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    box {| key = "loop-review"
           description = "Pre-review task description with a reviewer sub-agent, then activate review loop mode."
           inputHint = "<task description>"
           execute = System.Func<string, string, JS.Promise<string>>(loopReviewExecute deps reviewStore) |}

let createSlashCommands (deps: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj array =
    [| createLoopOnlyCommand reviewStore; createLoopReviewCommand deps reviewStore |]
