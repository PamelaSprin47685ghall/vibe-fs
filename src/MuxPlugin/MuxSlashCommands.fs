module VibeFs.MuxPlugin.MuxSlashCommands

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.AgentRole
open VibeFs.Kernel.AgentPolicy
open VibeFs.Kernel.HostKernel
open VibeFs.Kernel.PlanTypes
open VibeFs.Kernel.PlanEngine
open VibeFs.MuxPlugin.Delegate
open VibeFs.Shell.Write

let private rng = System.Random()

let private dateNow () : int = int (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
[<Global("process")>]
let private nodeProcess : obj = jsNative

let private processCwd () : string = nodeProcess?cwd()
let private randomHex4 () : string = sprintf "%04x" (rng.Next(65536))
let private fallbackSlashConfig (deps: obj) (workspaceId: string) : obj =
    createObj
        [ "cwd", box (processCwd ())
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

/// /loop: activate review loop mode.
let createLoopOnlyCommand (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : obj =
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

let private reviewPromptFor (task: string) : string =
    "You are a reviewer evaluating whether a task description is clear and actionable enough to begin work.\n\n"
    + "=== Task Description ===\n\n" + task + "\n\n"
    + "Evaluate the task description above. If it is clear, specific, and actionable, respond with exactly: PASS\n"
    + "If the task description has issues (ambiguous, missing requirements, contradictory), provide specific, actionable feedback."

/// /loop-review: pre-review task description with a reviewer sub-agent, then activate review loop mode.
let createLoopReviewCommand (deps: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : obj =
    box {| key = "loop-review"
           description = "Pre-review task description with a reviewer sub-agent, then activate review loop mode."
           inputHint = "<task description>"
           execute = System.Func<string, string, JS.Promise<string>>(fun workspaceId args ->
                let task = args.Trim()
                if task = "" then
                    reviewStore.deactivateReview workspaceId
                    (async { return "Loop mode cancelled." } |> Async.StartAsPromise)
                elif reviewStore.isReviewActive workspaceId then
                    (async { return "Loop mode is already active. Submit your work via submit_review." } |> Async.StartAsPromise)
                else
                    async {
                        let! config = pluginConfigForSlash deps workspaceId |> Async.AwaitPromise
                        let experiments = createObj [ "subagentRole", box "reviewer"; "toolPolicy", box (createObj [ "disabledTools", box ((subagentToolPolicy Reviewer).disabledTools |> Array.ofList) ]) ]
                        let reviewPrompt = reviewPromptFor task
                        let opts = createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]
                        let! outcome = delegateWithTimeout deps config "explore" reviewPrompt "Pre-review" (Some opts) 300000
                        match outcome with
                        | TimedOut ->
                            return buildLoopMessage task [ "Loop mode was NOT activated because the pre-review timed out. Please retry /loop-review." ]
                        | Report report ->
                            reviewStore.activateReview(workspaceId, task, dateNow ())
                            let trimmed = report.Trim()
                            let isReject = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\b(REJECT|FAIL|DENIED|DO NOT ACCEPT)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                            let passMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"\bPASS\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                            let isPass =
                                if isReject then false
                                elif passMatch.Success && passMatch.Index < 200 then
                                    let afterPass = trimmed.Substring(passMatch.Index + 4)
                                    not (System.Text.RegularExpressions.Regex.IsMatch(afterPass, @"\bFAIL\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                                else false
                            return
                                if isPass then
                                    buildLoopMessage task [ "Loop mode is active. Pre-review passed. Complete the task above, then call submit_review with:" ]
                                else
                                    buildLoopMessage task [ "Pre-review feedback:"; ""; trimmed; ""; "Loop mode is active. Address the pre-review feedback above while completing the task. Then call submit_review with:" ]
                    } |> Async.StartAsPromise) |}

let private planFooter =
    "\n\nYou must output ONLY the JSON requested. Do not write files, run commands, or modify the workspace."

/// /plan: generate a structured plan file via multi-branch reasoning.
let createPlanCommand (deps: obj) : obj =
    box {| key = "plan"
           description = "Generate a structured plan file via multi-branch reasoning."
           inputHint = "<requirement>"
           execute = System.Func<string, string, JS.Promise<string>>(fun workspaceId args ->
                async {
                    let! config = pluginConfigForSlash deps workspaceId |> Async.AwaitPromise
                    let rawRequirement = args.Trim()
                    if rawRequirement = "" then
                        return "Please provide a requirement, e.g. /plan design a login flow."
                    else
                        let hex4 = randomHex4 ()
                        let directory = Dyn.str config "cwd"
                        let request =
                            { requestId = workspaceId + "-" + hex4
                              rawRequirement = rawRequirement
                              normalizedRequirement = normalizeRequirement rawRequirement
                              branchCount = 3
                              branchModelName = "plan"
                              judgeModelName = "reviewer"
                              outputFileName = formatPlanFileName hex4
                              workspaceRoot = directory
                              existingContext = None }
                        let branchOpts = createObj [ "aiSettingsAgentId", box request.branchModelName; "subagentRole", box "reverie" ]
                        let judgeOpts = createObj [ "aiSettingsAgentId", box request.judgeModelName; "subagentRole", box "reviewer" ]
                        let branchCaller prompt =
                            async {
                                let! outcome = delegateWithTimeout deps config "explore" (prompt + planFooter) "Plan branch" (Some branchOpts) 300000
                                match outcome with Report s -> return s | TimedOut -> return "{}"
                            }
                        let judgeCaller prompt =
                            async {
                                let! outcome = delegateWithTimeout deps config "explore" (prompt + planFooter) "Plan judge" (Some judgeOpts) 300000
                                match outcome with Report s -> return s | TimedOut -> return "{}"
                            }
                        let! result = runPlanPipeline request branchCaller judgeCaller
                        let! writeMsg = write (Some directory) result.finalFileName result.finalMarkdown
                        return $"Plan written to {result.finalFileName}\n\n{writeMsg}"
                } |> Async.StartAsPromise) |}

/// Build all slash commands.
let createSlashCommands (deps: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : obj array =
    [| createLoopOnlyCommand reviewStore; createLoopReviewCommand deps reviewStore; createPlanCommand deps |]
