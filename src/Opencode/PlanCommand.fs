module VibeFs.Opencode.PlanCommand

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.PlanTypes
open VibeFs.Kernel.PlanEngine
open VibeFs.Opencode.Session
open VibeFs.Opencode.Sdk
open VibeFs.Shell.Write

open System.Collections.Generic

let private rng = System.Random()

let private randomHex4 () : string = sprintf "%04x" (rng.Next(65536))

let private pushPart (arr: obj) (part: obj) : unit =
    (arr :?> ResizeArray<obj>).Add(part)

let private ensureParts (output: obj) : obj =
    let parts = Dyn.get output "parts"
    if Dyn.isNullish parts then
        let arr = ResizeArray<obj>()
        output?("parts") <- box arr
        box arr
    else
        parts

/// Build an opencode tool object that captures its arguments into a shared list
/// and reports the call using the canonical plan tool name.
let private planTool (calls: PlanToolCall list ref) (schema: PlanToolSchema) : obj =
    define schema.description schema.parameters
        (fun args _ ->
            calls.Value <- { toolName = schema.name; arguments = args } :: calls.Value
            async { return "Submitted." } |> Async.StartAsPromise)

/// Build the JS tool map used for a plan stage. The map is keyed by tool name.
let private planToolsObj (calls: PlanToolCall list ref) (schemas: PlanToolSchema list) : obj =
    let o = createObj []
    for schema in schemas do
        o?(schema.name) <- planTool calls schema
    o

/// Call a plan model through an opencode subagent equipped with the requested tools.
let private callPlanModel
    (client: obj) (agent: string) (title: string) (directory: string) (sessionID: string)
    (prompt: string) (schemas: PlanToolSchema list) : Async<PlanToolCall list> =
    async {
        let calls = ref []
        let tools = planToolsObj calls schemas
        let! _, captured = runSubagentWithTools client agent title prompt directory sessionID null tools |> Async.AwaitPromise
        let capturedToolCalls =
            captured
            |> List.choose (fun c ->
                let toolName = Dyn.str c "toolName"
                let arguments = Dyn.get c "arguments"
                if toolName = "" || Dyn.isNullish arguments then None else Some { toolName = toolName; arguments = arguments })
        return if List.isEmpty capturedToolCalls then calls.Value else capturedToolCalls
    }

let handlePlanCommand (ctx: obj) (input: obj) (output: obj) : Async<unit> =
    async {
        let command = Dyn.str input "command"
        if command = "plan" then
            let rawRequirement = (Dyn.str input "arguments").Trim()
            let directory = Dyn.str ctx "directory"
            let sessionID = Dyn.str input "sessionID"
            let client = Dyn.get ctx "client"
            let parts = ensureParts output
            if rawRequirement = "" then
                pushPart parts (box {| ``type`` = "text"; text = "Please provide a requirement, e.g. /plan design a login flow." |})
            else
                let hex4 = randomHex4 ()
                let request =
                    { requestId = sessionID + "-" + hex4
                      rawRequirement = rawRequirement
                      normalizedRequirement = PlanEngine.normalizeRequirement rawRequirement
                      branchCount = 5
                      branchModelName = "reverie"
                      judgeModelName = "reviewer"
                      outputFileName = PlanEngine.formatPlanFileName hex4
                      workspaceRoot = directory
                      existingContext = None }
                let branchAgent = if request.branchModelName = "" then "reverie" else request.branchModelName
                let judgeAgent = if request.judgeModelName = "" then "reviewer" else request.judgeModelName
                let branchCaller prompt schemas = callPlanModel client branchAgent "Plan branch" directory sessionID prompt schemas
                let judgeCaller prompt schemas = callPlanModel client judgeAgent "Plan judge" directory sessionID prompt schemas
                let hypothesisCaller = Some(branchCaller)
                let! result = PlanEngine.runPlanPipeline request branchCaller judgeCaller hypothesisCaller
                let! writeMsg = write (Some directory) result.finalFileName result.finalMarkdown
                pushPart parts (box {| ``type`` = "text"; text = $"Plan written to {result.finalFileName}\n\n{writeMsg}" |})
    }
