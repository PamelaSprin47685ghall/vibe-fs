module VibeFs.MuxPlugin.PlanTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.PlanTypes
open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.PlanToolStore

module private Dyn =
    let isNullish (value: obj) : bool = isNullOrUndefined value
    let get (o: obj) (key: string) : obj = o?(key)
    let clone (o: obj) : obj = JS.JSON.parse(JS.JSON.stringify(o))
    let isArray (value: obj) : bool =
        if isNullish value then false
        else JS.Constructors.Array.isArray(value)

let private strField (a: obj) (k: string) : string option =
    let v = Dyn.get a k
    if Dyn.isNullish v then None else Some(string v)

let private resolveStr (s: string) : JS.Promise<string> = async { return s } |> Async.StartAsPromise
let private objectKeys (o: obj) : string array = JS.Constructors.Object.keys(o) |> Seq.toArray
let private stringifyPretty (o: obj) : string = JS.JSON.stringify(o)

/// Names of plan tools that are registered for use by /plan subagents.
let planToolNames =
    [ "submit_plan_hypotheses"
      "submit_plan_branch"
      "submit_plan_critique"
      "submit_plan_pool"
      "submit_plan_revision"
      "submit_plan_judge" ]

let private planToolName (schema: PlanToolSchema) : string = schema.name

let private requireCallId (args: obj) : string =
    defaultArg (strField args "callId") ""

let private makePlanTool (schema: PlanToolSchema) : ToolDefinition =
    let parameters = Dyn.clone schema.parameters
    let properties = Dyn.get parameters "properties"
    if not (Dyn.isNullish properties) then
        properties?("callId") <- createObj [ "type", box "string"; "description", box "Internal call id supplied in the prompt." ]
    let required =
        let r = Dyn.get parameters "required"
        let existing =
            if Dyn.isArray r then
                (r :?> obj[]) |> Array.map string
            else
                [||]
        Array.append existing [| "callId" |]
    parameters?("required") <- required
    parameters?("additionalProperties") <- false
    let propertiesObj = JS.JSON.parse(JS.JSON.stringify(properties))
    let requiredStrings = required |> Array.map (fun x -> string x)
    { name = schema.name
      description = schema.description + " This is an internal /plan tool."
      parameters =
        { ``type`` = "object"
          properties = propertiesObj
          required = Some requiredStrings
          additionalProperties = Some false }
      execute = fun _ args ->
          let callId = requireCallId args
          if callId = "" then
              resolveStr "Missing callId"
          elif resolveCall callId args then
              resolveStr "Submitted."
          else
              resolveStr $"No pending plan call for {callId}"
      condition = None }

/// All plan tool definitions, keyed by name.
let planToolDefinitions : Map<string, ToolDefinition> =
    [ PlanHypotheses.buildPlanHypothesesToolSchema
      PlanBranches.buildPlanBranchToolSchema
      PlanCritique.buildPlanCritiqueToolSchema
      PlanPool.buildPlanPoolToolSchema
      PlanRevision.buildPlanRevisionToolSchema
      PlanJudge.buildPlanJudgeToolSchema ]
    |> List.map (fun schema -> planToolName schema, makePlanTool schema)
    |> Map.ofList

/// Array of all plan tools for registration.
let allPlanTools : ToolDefinition array =
    planToolDefinitions |> Map.toArray |> Array.map snd

let private buildAgentReportProperties () : obj =
    let properties =
        createObj
            [ "reportMarkdown", box (createObj [ "type", box "string"; "description", box "Human-friendly markdown shown in the upstream UI." ])
              "callId", box (createObj [ "type", box "string"; "description", box "Internal plan call id supplied by the prompt." ]) ]
    for schema in [ PlanHypotheses.buildPlanHypothesesToolSchema
                    PlanBranches.buildPlanBranchToolSchema
                    PlanCritique.buildPlanCritiqueToolSchema
                    PlanPool.buildPlanPoolToolSchema
                    PlanRevision.buildPlanRevisionToolSchema
                    PlanJudge.buildPlanJudgeToolSchema ] do
        let schemaProps = Dyn.get schema.parameters "properties"
        if not (Dyn.isNullish schemaProps) then
            for key in objectKeys schemaProps do
                properties?(key) <- Dyn.get schemaProps key
    properties

let fakeAgentReportDefinition : ToolDefinition =
    { name = "agent_report"
      description = "Submit structured work results. For /plan flows, provide the stage fields directly plus callId; the plugin forwards a markdown rendering to the upstream UI."
      parameters =
          { ``type`` = "object"
            properties = buildAgentReportProperties ()
            required = Some [| "callId" |]
            additionalProperties = Some false }
      execute = fun _config args ->
          let callId = requireCallId args
          if callId = "" then
              resolveStr (defaultArg (strField args "reportMarkdown") "")
          elif resolveCall callId args then
              resolveStr "Submitted."
          else
              resolveStr $"No pending plan call for {callId}"
      condition = None }

let formatAgentReportMarkdown (args: obj) : string =
    let copied = Dyn.clone args
    copied?("callId") <- null
    let content = stringifyPretty copied
    "# Agent Report\n\n```json\n" + content + "\n```"
