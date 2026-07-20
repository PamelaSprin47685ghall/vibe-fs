module Wanxiangshu.Runtime.ToolHookRuntime

open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.DynField
open Wanxiangshu.Runtime.ToolSchemaRegistry
open Wanxiangshu.Runtime.ToolArgumentCoercion
open Wanxiangshu.Runtime.ToolHookIdentity

type SchemaType = ToolSchemaRegistry.SchemaType
type ControlEnvelope = ToolSchemaRegistry.ControlEnvelope
type ToolCapability = ToolArgumentCoercion.ToolCapability

let registerToolParameterTypes entries = ToolSchemaRegistry.registerToolParameterTypes entries
let removeRegistryForTool tool = ToolSchemaRegistry.removeRegistryForTool tool
let extractSchemaTypes schema = ToolSchemaRegistry.extractSchemaTypes schema
let getSessionCancelGeneration sessionID = ToolSchemaRegistry.getSessionCancelGeneration sessionID
let incrementSessionCancelGeneration sessionID = ToolSchemaRegistry.incrementSessionCancelGeneration sessionID
let saveCompliance sessionID toolCallID env = ToolSchemaRegistry.saveCompliance sessionID toolCallID env
let tryGetCompliance sessionID toolCallID = ToolSchemaRegistry.tryGetCompliance sessionID toolCallID
let removeCompliance sessionID toolCallID = ToolSchemaRegistry.removeCompliance sessionID toolCallID
let closeSession sessionID = ToolSchemaRegistry.closeSession sessionID
let clearSessionCompliance sessionID = ToolSchemaRegistry.clearSessionCompliance sessionID
let getToolCapabilities toolName = ToolArgumentCoercion.getToolCapabilities toolName
let parseSchemaToTypes tool field = ToolArgumentCoercion.parseSchemaToTypes tool field
let tryGetRequiredFields toolName = ToolArgumentCoercion.tryGetRequiredFields toolName
let sanitizeNullArgs toolName args = ToolArgumentCoercion.sanitizeNullArgs toolName args
let canonicalToolName toolName = ToolArgumentCoercion.canonicalToolName toolName
let tryCoerceStringValue expected s = ToolArgumentCoercion.tryCoerceStringValue expected s
let coerceArgsTypes toolName args = ToolArgumentCoercion.coerceArgsTypes toolName args
let decorateAndValidateSchema toolName schema = ToolArgumentCoercion.decorateAndValidateSchema toolName schema
let registerSchemaTypes toolName schema = ToolArgumentCoercion.registerSchemaTypes toolName schema

let tryExtractToolCallId = ToolHookIdentity.tryExtractToolCallId
let tryExtractSessionId = ToolHookIdentity.tryExtractSessionId

let restoreWarnToArgs (args: obj) (env: ControlEnvelope) : unit =
    if not (Dyn.isNullish args) then
        match env.WarnTdd with
        | Some v -> args?("warn_tdd") <- box v
        | None -> ()

        match env.Warn with
        | Some v -> args?("warn") <- box v
        | None -> ()

        match env.WarnReuse with
        | Some v -> args?("warn_reuse") <- box v
        | None -> ()

[<RequireQualifiedAccess>]
type ExecutionStatus =
    | Success
    | Failure
    | Cancelled

let appendCriticism (output: string) (_violations: string list) (_status: ExecutionStatus) : string = output

let private extractControlFields (args: obj) =
    let getField k =
        match DynField.strField args k with
        | Some v ->
            Dyn.deleteKey args k
            Some v
        | None -> None

    getField "warn_tdd", getField "warn", getField "warn_reuse"

let executeBeforeGateway (tool: string) (args: obj) : Result<obj * ControlEnvelope, string> =
    if Dyn.isNullish args then
        Result.Ok(
            createObj [],
            { WarnTdd = None
              Warn = None
              WarnReuse = None
              Violations = []
              GenerationAtStart = 0
              SessionId = "" }
        )
    else
        coerceArgsTypes tool args
        sanitizeNullArgs tool args
        let caps = getToolCapabilities tool
        let warnTddVal, warnVal, warnReuseVal = extractControlFields args
        let hasControlFields = warnTddVal.IsSome || warnVal.IsSome || warnReuseVal.IsSome

        let nextArgs =
            if not caps.IsEmpty || hasControlFields then
                Dyn.cloneShallow args
            else
                args

        let env =
            { WarnTdd = warnTddVal
              Warn = warnVal
              WarnReuse = warnReuseVal
              Violations = []
              GenerationAtStart = 0
              SessionId = "" }

        Result.Ok(nextArgs, env)
