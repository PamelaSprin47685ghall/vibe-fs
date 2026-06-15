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

/// Validate that all required fields declared in the JSON schema are present and
/// non-null on the captured arguments object. Returns the first missing field.
let private validateRequiredArgs (schema: PlanToolSchema) (args: obj) : Result<unit, string> =
    let required = Dyn.get schema.parameters "required"
    if not (Dyn.isArray required) then Ok ()
    else
        let keys = unbox<obj[]> required |> Array.map string
        keys
        |> Array.tryFind (fun k ->
            let v = Dyn.get args k
            Dyn.isNullish v)
        |> function
            | None -> Ok ()
            | Some k -> Error $"Missing required field '{k}' for tool '{schema.name}'"

/// Convert a JSON schema object to a ZodRawShape usable by the opencode SDK tool factory.
let rec private jsonSchemaToZodType (schemaObj: obj) : obj =
    match Dyn.str schemaObj "type" with
    | "string" -> call0 schema "string"
    | "number" -> call0 schema "number"
    | "boolean" -> call0 schema "boolean"
    | "array" ->
        let items = Dyn.get schemaObj "items"
        let itemType = if Dyn.isNullish items then call0 schema "string" else jsonSchemaToZodType items
        call1 schema "array" itemType
    | "object" -> call1 schema "object" (jsonSchemaToZodShape schemaObj)
    | _ -> call0 schema "string"

and private jsonSchemaToZodShape (schemaObj: obj) : obj =
    let props = Dyn.get schemaObj "properties"
    let required =
        let r = Dyn.get schemaObj "required"
        if Dyn.isArray r then (r :?> obj[]) |> Array.map string |> Set.ofArray else Set.empty
    let shape = createObj []
    if not (Dyn.isNullish props) then
        let keys = JS.Constructors.Object.keys(props) |> Seq.toArray
        for k in keys do
            let baseType = jsonSchemaToZodType (Dyn.get props k)
            let final = if Set.contains k required then baseType else call0 baseType "optional"
            shape?(k) <- final
    shape

let private disabledInPlanMode _ _ = async { return "disabled in plan mode" } |> Async.StartAsPromise

let private disabledToolNames =
    [ "editor"; "greper"; "reverie"; "browser"; "executor"; "fuzzy_find"; "fuzzy_grep"
      "websearch"; "webfetch"; "submit_review"; "read"; "write"; "edit"; "bash"; "task" ]

let private addDisabledToolOverrides (tools: obj) : obj =
    let emptyShape = createObj []
    for name in disabledToolNames do
        tools?(name) <- define "disabled in plan mode" emptyShape disabledInPlanMode
    tools

/// Build an opencode tool object that captures its arguments into a shared list
/// and reports the call using the canonical plan tool name.
let private planTool (calls: PlanToolCall list ref) (schema: PlanToolSchema) : obj =
    let args = jsonSchemaToZodShape schema.parameters
    define schema.description args
        (fun capturedArgs _ ->
            match validateRequiredArgs schema capturedArgs with
            | Error message ->
                async { return $"[TOOL_ERROR: {message}]" } |> Async.StartAsPromise
            | Ok () ->
                calls.Value <- { toolName = schema.name; arguments = capturedArgs } :: calls.Value
                async { return "Submitted." } |> Async.StartAsPromise)

/// Build the JS tool map used for a plan stage. The map is keyed by tool name.
let private planToolsObj (calls: PlanToolCall list ref) (schemas: PlanToolSchema list) : obj =
    let o = createObj []
    for schema in schemas do
        o?(schema.name) <- planTool calls schema
    o

/// Call a plan model through an opencode subagent equipped with the requested tools.
/// Returns the captured tool calls. If no tool was called but the subagent produced
/// text, returns a synthetic error so the pipeline does not silently use empty data.
let private callPlanModel
    (client: obj) (agent: string) (title: string) (directory: string) (sessionID: string)
    (abortSignal: obj) (prompt: string) (schemas: PlanToolSchema list) : Async<PlanToolCall list> =
    async {
        let calls = ref []
        let tools = planToolsObj calls schemas |> addDisabledToolOverrides
        let context = if Dyn.isNullish abortSignal then box null else box {| abort = abortSignal |}
        let! text = runSubagentWithTools client agent title prompt directory sessionID context tools |> Async.AwaitPromise
        return
            if List.isEmpty calls.Value && not (System.String.IsNullOrWhiteSpace text) && text <> "(no output)" && text <> "(aborted)" then
                [ { toolName = "_plan_subagent_no_tool_"; arguments = box {| text = text |} } ]
            else
                calls.Value
    }

let handlePlanCommand (ctx: obj) (input: obj) (output: obj) : Async<unit> =
    async {
        let command = Dyn.str input "command"
        if command = "plan" then
            let rawRequirement = (Dyn.str input "arguments").Trim()
            let directory = Dyn.str ctx "directory"
            let sessionID = Dyn.str input "sessionID"
            let client = Dyn.get ctx "client"
            let abortSignal = Dyn.get input "abort"
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
                let branchCaller prompt schemas = callPlanModel client branchAgent "Plan branch" directory sessionID abortSignal prompt schemas
                let judgeCaller prompt schemas = callPlanModel client judgeAgent "Plan judge" directory sessionID abortSignal prompt schemas
                let hypothesisCaller = Some(branchCaller)
                let! result = PlanEngine.runPlanPipeline request branchCaller judgeCaller hypothesisCaller
                let! actualFileName, writeMsg = writeUnique (Some directory) result.finalFileName result.finalMarkdown 100
                pushPart parts (box {| ``type`` = "text"; text = $"Plan written to {actualFileName}\n\n{writeMsg}" |})
    }
