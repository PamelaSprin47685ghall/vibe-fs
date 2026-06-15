module VibeFs.MuxPlugin.MuxSlashCommands

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.AgentRole
open VibeFs.Kernel.AgentPolicy
open VibeFs.Kernel.HostKernel
open VibeFs.Kernel.PlanTypes
open VibeFs.Kernel.PlanEngine
open VibeFs.Kernel.PlanCommon
open VibeFs.MuxPlugin.Delegate
open VibeFs.MuxPlugin.PlanTools
open VibeFs.MuxPlugin.PlanToolStore
open VibeFs.Shell.Write

let private rng = System.Random()

let mutable private dateNowSource = fun () -> System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
let private dateNow () : int64 = dateNowSource ()

/// Replace the clock used by /loop and /loop-review for deterministic tests.
let setDateNowSource (source: unit -> int64) : unit = dateNowSource <- source

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

let private planInstructionFooter (toolName: string) (callId: string) =
    "\n\nYou must call the `" + toolName + "` tool to submit your answer. "
    + "Use callId `" + callId + "`. Do not write files, run commands, or modify the workspace."

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
                        let disabledTools = (subagentToolPolicy Reviewer).disabledTools @ planToolNames
                        let callId = workspaceId + "-loop-review-" + string (dateNow ())
                        let verdictPromise = registerCallWithTimeout callId 300000
                        let experiments =
                            createObj
                                [ "subagentRole", box "reviewer"
                                  "toolPolicy", box (createObj [ "disabledTools", box (disabledTools |> Array.ofList); "allowedTools", box [| "agent_report" |] ]) ]
                        let opts = createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]
                        let! outcome = delegateWithTimeout deps config "explore" (loopReviewVerdictInstructions + "\n\n=== Task Description ===\n\n" + task + "\n\n" + planInstructionFooter "agent_report" callId) "Pre-review" (Some opts) 300000
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
                    } |> Async.StartAsPromise) |}


let private toolPolicyForPlan () : obj =
    createObj
        [ "disabledTools", box [||]
          "allowedTools", box [| "agent_report" |] ]

let private callPlanModel
    (deps: obj) (config: obj) (agentId: string) (title: string) (prompt: string)
    (aiSettingsAgentId: string) (toolSchemas: PlanToolSchema list) (callId: string)
    : Async<PlanToolCall list> =
    async {
        match toolSchemas with
        | [] -> return []
        | primarySchema :: _ ->
            let registerPromise = registerCall callId
            let experiments =
                createObj
                    [ "aiSettingsAgentId", box aiSettingsAgentId
                      "subagentRole", box "reverie"
                      "toolPolicy", toolPolicyForPlan () ]
            let opts = createObj [ "aiSettingsAgentId", box aiSettingsAgentId; "experiments", box experiments ]
            let! outcome = delegateWithTimeout deps config agentId (prompt + planInstructionFooter "agent_report" callId) title (Some opts) 300000
            match outcome with
            | TimedOut -> return []
            | Report _ ->
                let! args = registerPromise |> Async.AwaitPromise
                return [ { toolName = primarySchema.name; arguments = args } ]
    }

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
                              normalizedRequirement = PlanCommon.normalizeRequirement rawRequirement
                              branchCount = 5
                              branchModelName = "exec"
                              judgeModelName = "plan"
                              outputFileName = PlanCommon.formatPlanFileName hex4
                              workspaceRoot = directory
                              existingContext = None }
                        let mutable callCounter = 0
                        let nextCallId () =
                            callCounter <- callCounter + 1
                            workspaceId + "-plan-" + hex4 + "-" + string callCounter
                        let branchCaller prompt schemas =
                            async {
                                let! calls = callPlanModel deps config "explore" "Plan branch" prompt request.branchModelName schemas (nextCallId ())
                                return calls
                            }
                        let judgeCaller prompt schemas =
                            async {
                                let! calls = callPlanModel deps config "explore" "Plan judge" prompt request.judgeModelName schemas (nextCallId ())
                                return calls
                            }
                        let hypothesisCaller = Some branchCaller
                        let! result = runPlanPipeline request branchCaller judgeCaller hypothesisCaller
                        let! actualFileName, writeMsg = VibeFs.Shell.Write.writeUnique (Some directory) result.finalFileName result.finalMarkdown 100
                        return $"Plan written to {actualFileName}\n\n{writeMsg}"
                } |> Async.StartAsPromise) |}


/// Build all slash commands.
let createSlashCommands (deps: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : obj array =
    [| createLoopOnlyCommand reviewStore; createLoopReviewCommand deps reviewStore; createPlanCommand deps |]
