module Wanxiangshu.Runtime.ToolHookRuntime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.DynField
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.ToolContextCodec
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Runtime.ToolSchemaRegistry
open Wanxiangshu.Runtime.ToolArgumentCoercion

type SchemaType = ToolSchemaRegistry.SchemaType

let registerToolParameterTypes entries =
    ToolSchemaRegistry.registerToolParameterTypes entries

let removeRegistryForTool tool =
    ToolSchemaRegistry.removeRegistryForTool tool

let extractSchemaTypes schema =
    ToolSchemaRegistry.extractSchemaTypes schema

let getSessionCancelGeneration sessionID =
    ToolSchemaRegistry.getSessionCancelGeneration sessionID

let incrementSessionCancelGeneration sessionID =
    ToolSchemaRegistry.incrementSessionCancelGeneration sessionID

type ControlEnvelope = ToolSchemaRegistry.ControlEnvelope

let saveCompliance sessionID toolCallID env =
    ToolSchemaRegistry.saveCompliance sessionID toolCallID env

let tryGetCompliance sessionID toolCallID =
    ToolSchemaRegistry.tryGetCompliance sessionID toolCallID

let removeCompliance sessionID toolCallID =
    ToolSchemaRegistry.removeCompliance sessionID toolCallID

let closeSession sessionID =
    ToolSchemaRegistry.closeSession sessionID

let clearSessionCompliance sessionID =
    ToolSchemaRegistry.clearSessionCompliance sessionID

type ToolCapability = ToolArgumentCoercion.ToolCapability

let getToolCapabilities toolName =
    ToolArgumentCoercion.getToolCapabilities toolName

let parseSchemaToTypes tool field =
    ToolArgumentCoercion.parseSchemaToTypes tool field

let tryGetRequiredFields toolName =
    ToolArgumentCoercion.tryGetRequiredFields toolName

let sanitizeNullArgs toolName args =
    ToolArgumentCoercion.sanitizeNullArgs toolName args

let canonicalToolName toolName =
    ToolArgumentCoercion.canonicalToolName toolName

let tryCoerceStringValue expected s =
    ToolArgumentCoercion.tryCoerceStringValue expected s

let coerceArgsTypes toolName args =
    ToolArgumentCoercion.coerceArgsTypes toolName args

let decorateAndValidateSchema toolName schema =
    ToolArgumentCoercion.decorateAndValidateSchema toolName schema

let registerSchemaTypes toolName schema =
    ToolArgumentCoercion.registerSchemaTypes toolName schema

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

let tryExtractToolCallId (input: obj) : string option =
    if Dyn.isNullish input then
        None
    else
        let getField k obj =
            match DynField.strField obj k with
            | Some id when id <> "" -> Some id
            | _ -> None

        match getField "toolCallId" input with
        | Some id -> Some id
        | None ->
            match getField "callId" input with
            | Some id -> Some id
            | None ->
                match getField "callID" input with
                | Some id -> Some id
                | None ->
                    let tool = Dyn.get input "tool"

                    if not (Dyn.isNullish tool) && Dyn.typeIs tool "object" then
                        match getField "callID" tool with
                        | Some id -> Some id
                        | None ->
                            match getField "callId" tool with
                            | Some id -> Some id
                            | None -> getField "toolCallId" tool
                    else
                        None

let tryExtractSessionId (input: obj) : string option =
    if Dyn.isNullish input then
        None
    else
        let getField k obj =
            match DynField.strField obj k with
            | Some id when id <> "" -> Some id
            | _ -> None

        match getField "sessionID" input with
        | Some id -> Some id
        | None ->
            match getField "sessionId" input with
            | Some id -> Some id
            | None ->
                match getField "session_id" input with
                | Some id -> Some id
                | None ->
                    match getField "workspaceId" input with
                    | Some id -> Some id
                    | None ->
                        let s = Dyn.get input "session"

                        if not (Dyn.isNullish s) && Dyn.typeIs s "object" then
                            getField "id" s
                        else
                            None

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
