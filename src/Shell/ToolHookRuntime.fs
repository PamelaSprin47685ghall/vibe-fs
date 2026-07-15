module Wanxiangshu.Shell.ToolHookRuntime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.DynField
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.ToolContextCodec
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.ToolCatalog
open Thoth.Json

/// Schema-level type for a single tool parameter field.
/// Used by the generic coerceArgsTypes to replace the ad-hoc whitelist.
type SchemaType =
    | SString
    | SNumber
    | SBoolean
    | SArray
    | SObject

let private registry = ResizeArray<string * string * SchemaType>()

/// Register the parameter types for a given tool.
/// Each entry is (toolName, fieldName, schemaType).
let registerToolParameterTypes (entries: (string * string * SchemaType) list) : unit =
    for entry in entries do
        registry.Add entry

let private removeRegistryForTool (tool: string) : unit =
    let mutable i = registry.Count - 1

    while i >= 0 do
        let (t, _, _) = registry.[i]

        if t = tool then
            registry.RemoveAt i

        i <- i - 1

let private schemaTypeOfJsonField (fieldSchema: obj) : SchemaType option =
    if Dyn.isNullish fieldSchema then
        None
    else
        let rawType = Dyn.get fieldSchema "type"

        let typeName =
            if Dyn.isNullish rawType then
                ""
            elif Dyn.isArray rawType then
                let arr = unbox<obj array> rawType

                if arr.Length > 0 then string arr.[0] else ""
            else
                string rawType

        match typeName with
        | "integer"
        | "number" -> Some SNumber
        | "boolean" -> Some SBoolean
        | "array" -> Some SArray
        | "object" -> Some SObject
        | "string" -> Some SString
        | _ -> None

/// Walk a JSON-schema-shaped object and collect (fieldName, SchemaType) pairs.
let extractSchemaTypes (schema: obj) : (string * SchemaType) list =
    if Dyn.isNullish schema then
        []
    else
        let props = Dyn.get schema "properties"

        if Dyn.isNullish props || not (Dyn.typeIs props "object") then
            []
        else
            Dyn.keys props
            |> Array.choose (fun key ->
                match schemaTypeOfJsonField (Dyn.get props key) with
                | Some st -> Some(key, st)
                | None -> None)
            |> Array.toList

/// Look up the registered SchemaType for a (tool, field) pair.
let parseSchemaToTypes (tool: string) (field: string) : SchemaType option =
    registry
    |> Seq.tryPick (fun (t, f, s) -> if t = tool && f = field then Some s else None)

/// Look up the required field names for a given tool from ToolSpec.
/// Falls back to empty list for unknown tools.
let tryGetRequiredFields (toolName: string) : string list =
    if toolName = "methodology" || toolName.StartsWith("methodology_") then
        [ "methodology"; "intent"; "background"; "note" ]
    else
        match specOf toolName with
        | Ok spec -> spec.requiredFields
        | Error _ -> []

/// Remove null/undefined and empty object {} values from args for keys that are NOT required fields.
/// This prevents downstream tool execution from seeing spurious null/{} values
/// that the LLM filler injected for optional parameters.
let sanitizeNullArgs (toolName: string) (args: obj) : unit =
    if not (Dyn.isNullish args) && Dyn.typeIs args "object" then
        let req = tryGetRequiredFields toolName |> Set.ofList

        for k in Dyn.keys args do
            let v = Dyn.get args k
            let isNullish = Dyn.isNullish v

            let isEmptyObject =
                not isNullish
                && Dyn.typeIs v "object"
                && not (Dyn.isArray v)
                && (Dyn.keys v).Length = 0

            if (isNullish || isEmptyObject) && not (Set.contains k req) then
                Dyn.deleteKey args k

type ToolCapability =
    | FileMutation
    | ProcessExecution
    | SubagentDelegation

let private sessionCancelGenerations =
    System.Collections.Generic.Dictionary<string, int>()

let private closedSessions = System.Collections.Generic.HashSet<string>()

let getSessionCancelGeneration (sessionID: string) : int =
    if System.String.IsNullOrWhiteSpace sessionID then
        0
    else
        match sessionCancelGenerations.TryGetValue(sessionID) with
        | true, g -> g
        | _ -> 0

let incrementSessionCancelGeneration (sessionID: string) : unit =
    if not (System.String.IsNullOrWhiteSpace sessionID) then
        match sessionCancelGenerations.TryGetValue(sessionID) with
        | true, g -> sessionCancelGenerations.[sessionID] <- g + 1
        | _ -> sessionCancelGenerations.[sessionID] <- 1

type ControlEnvelope =
    { WarnTdd: string option
      Warn: string option
      WarnReuse: string option
      Violations: string list
      mutable GenerationAtStart: int
      mutable SessionId: string }

    member this.Cancelled =
        this.GenerationAtStart < getSessionCancelGeneration this.SessionId

let private complianceStore =
    System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, ControlEnvelope>>()

let saveCompliance (sessionID: string) (toolCallID: string) (env: ControlEnvelope) : unit =
    if
        not (System.String.IsNullOrWhiteSpace sessionID)
        && not (System.String.IsNullOrWhiteSpace toolCallID)
    then
        env.SessionId <- sessionID
        env.GenerationAtStart <- getSessionCancelGeneration sessionID

        let innerStore =
            match complianceStore.TryGetValue(sessionID) with
            | true, store -> store
            | _ ->
                let store = System.Collections.Generic.Dictionary<string, ControlEnvelope>()
                complianceStore.[sessionID] <- store
                store

        innerStore.[toolCallID] <- env

let tryGetCompliance (sessionID: string) (toolCallID: string) : ControlEnvelope option =
    if
        System.String.IsNullOrWhiteSpace sessionID
        || System.String.IsNullOrWhiteSpace toolCallID
    then
        None
    else
        match complianceStore.TryGetValue(sessionID) with
        | true, innerStore ->
            match innerStore.TryGetValue(toolCallID) with
            | true, env -> Some env
            | _ -> None
        | _ -> None

let removeCompliance (sessionID: string) (toolCallID: string) : unit =
    if
        not (System.String.IsNullOrWhiteSpace sessionID)
        && not (System.String.IsNullOrWhiteSpace toolCallID)
    then
        match complianceStore.TryGetValue(sessionID) with
        | true, innerStore ->
            innerStore.Remove(toolCallID) |> ignore

            if innerStore.Count = 0 then
                complianceStore.Remove(sessionID) |> ignore

                if closedSessions.Contains(sessionID) then
                    sessionCancelGenerations.Remove(sessionID) |> ignore
                    closedSessions.Remove(sessionID) |> ignore
        | _ -> ()

let closeSession (sessionID: string) : unit =
    if not (System.String.IsNullOrWhiteSpace sessionID) then
        closedSessions.Add(sessionID) |> ignore

        let isEmpty =
            match complianceStore.TryGetValue(sessionID) with
            | true, innerStore -> innerStore.Count = 0
            | _ -> true

        if isEmpty then
            sessionCancelGenerations.Remove(sessionID) |> ignore
            closedSessions.Remove(sessionID) |> ignore

let clearSessionCompliance (sessionID: string) : unit =
    if not (System.String.IsNullOrWhiteSpace sessionID) then
        incrementSessionCancelGeneration sessionID

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

let reprimandMarker = "<WANXIANGSHU_COMPLIANCE_REPRIMAND>"

let appendCriticism (output: string) (violations: string list) (status: ExecutionStatus) : string =
    if violations.IsEmpty || (output <> null && output.Contains(reprimandMarker)) then
        output
    else
        let builder = System.Text.StringBuilder()
        builder.AppendLine() |> ignore
        builder.AppendLine(reprimandMarker) |> ignore
        builder.AppendLine("严重协议违例：") |> ignore

        for v in violations do
            builder.AppendLine("- " + v) |> ignore

        builder.AppendLine() |> ignore

        match status with
        | ExecutionStatus.Success -> builder.AppendLine("工具已经成功执行。不要重复本次工具调用。") |> ignore
        | ExecutionStatus.Failure
        | ExecutionStatus.Cancelled -> builder.AppendLine("本次工具调用已经结束，不要仅为补齐协议字段而机械重复调用。") |> ignore

        let criticism = builder.ToString()
        if output = null then criticism else output + criticism

let tryExtractToolCallId (input: obj) : string option =
    if Dyn.isNullish input then
        None
    else
        match DynField.strField input "toolCallId" with
        | Some id when id <> "" -> Some id
        | _ ->
            match DynField.strField input "callId" with
            | Some id when id <> "" -> Some id
            | _ ->
                match DynField.strField input "callID" with
                | Some id when id <> "" -> Some id
                | _ ->
                    let toolObj = Dyn.get input "tool"

                    if not (Dyn.isNullish toolObj) && Dyn.typeIs toolObj "object" then
                        match DynField.strField toolObj "callID" with
                        | Some id when id <> "" -> Some id
                        | _ ->
                            match DynField.strField toolObj "callId" with
                            | Some id when id <> "" -> Some id
                            | _ ->
                                match DynField.strField toolObj "toolCallId" with
                                | Some id when id <> "" -> Some id
                                | _ -> None
                    else
                        None

let tryExtractSessionId (input: obj) : string option =
    if Dyn.isNullish input then
        None
    else
        match DynField.strField input "sessionID" with
        | Some id when id <> "" -> Some id
        | _ ->
            match DynField.strField input "sessionId" with
            | Some id when id <> "" -> Some id
            | _ ->
                match DynField.strField input "session_id" with
                | Some id when id <> "" -> Some id
                | _ ->
                    match DynField.strField input "workspaceId" with
                    | Some id when id <> "" -> Some id
                    | _ ->
                        let sessionObj = Dyn.get input "session"

                        if not (Dyn.isNullish sessionObj) && Dyn.typeIs sessionObj "object" then
                            match DynField.strField sessionObj "id" with
                            | Some id when id <> "" -> Some id
                            | _ -> None
                        else
                            None

let getToolCapabilities (toolName: string) : ToolCapability list =
    let t = toolName.ToLowerInvariant().Trim()

    [ if
          t = "coder"
          || t = "edit"
          || t = "write"
          || t = "apply_patch"
          || t = "patch"
          || t = "ast_edit"
          || t = "ast_grep_replace"
          || t = "file_edit_replace_string"
          || t = "file_edit_insert"
          || t = "executor"
          || t.StartsWith("pty_")
      then
          yield FileMutation
      if t = "executor" || t.StartsWith("pty_") then
          yield ProcessExecution
      if t = "coder" || t = "investigator" || t = "meditator" || t = "browser" then
          yield SubagentDelegation ]

let inlineJsonWarnTddProperty: obj =
    createObj
        [ "type", box "string"
          "description",
          box "MUST acknowledge that tests are written first (TDD) and Kolmogorov discipline is followed."
          "x-wanxiangshu-soft-required", box true ]

let inlineJsonWarnProperty: obj =
    createObj
        [ "type", box "string"
          "description",
          box
              "MUST acknowledge that this task cannot be done with other tools and only run tests when static analysis cannot handle it."
          "x-wanxiangshu-soft-required", box true ]

let inlineJsonWarnReuseProperty: obj =
    createObj
        [ "type", box "string"
          "description", box "MUST acknowledge that this task is not suitable for completion via continue tool."
          "x-wanxiangshu-soft-required", box true ]

let private softenControlProperty (property: obj) : unit =
    if not (Dyn.isNullish property) then
        Dyn.deleteKey property "enum"
        Dyn.deleteKey property "const"
        Dyn.deleteKey property "pattern"
        Dyn.deleteKey property "minLength"
        Dyn.deleteKey property "maxLength"
        Dyn.setKey property "x-wanxiangshu-soft-required" true


let decorateAndValidateSchema (toolName: string) (schema: obj) : obj =
    if Dyn.isNullish schema then
        schema
    else
        let props = Dyn.get schema "properties"

        if Dyn.isNullish props then
            schema
        else
            let caps = getToolCapabilities toolName

            // Injections
            if List.contains FileMutation caps then
                if Dyn.isNullish (Dyn.get props "warn_tdd") then
                    Dyn.setKey props "warn_tdd" inlineJsonWarnTddProperty
                else
                    let prop = Dyn.get props "warn_tdd"

                    if not (Dyn.isNullish prop) then
                        softenControlProperty prop

            if List.contains ProcessExecution caps then
                if Dyn.isNullish (Dyn.get props "warn") then
                    Dyn.setKey props "warn" inlineJsonWarnProperty
                else
                    let prop = Dyn.get props "warn"

                    if not (Dyn.isNullish prop) then
                        softenControlProperty prop

            if List.contains SubagentDelegation caps then
                if Dyn.isNullish (Dyn.get props "warn_reuse") then
                    Dyn.setKey props "warn_reuse" inlineJsonWarnReuseProperty
                else
                    let prop = Dyn.get props "warn_reuse"

                    if not (Dyn.isNullish prop) then
                        softenControlProperty prop


            // Remove warn fields from required list to avoid host hard rejection
            let req = Dyn.get schema "required"

            if Dyn.isArray req then
                let arr = req :?> obj array

                let nextReq =
                    arr
                    |> Array.filter (fun x ->
                        let s = string x
                        s <> "warn_tdd" && s <> "warn" && s <> "warn_reuse")

                Dyn.setKey schema "required" nextReq

            // Soft validation: warn fields are soft-required per compliance policy.
            // Missing fields do NOT block tool registration or execution.
            // Only malformed business args, permission denial, parse failure,
            // or control field LEAKAGE are hard gates.
            let requiredList =
                let req = Dyn.get schema "required"

                if Dyn.isArray req then
                    (req :?> obj array) |> Array.map string |> Array.toList
                else
                    []

            for reqField in requiredList do
                if Dyn.isNullish (Dyn.get props reqField) then
                    Fable.Core.JS.console.warn (
                        $"Schema soft warning: required field '{reqField}' is not defined in properties of tool '{toolName}'"
                    )

            if List.contains FileMutation caps && Dyn.isNullish (Dyn.get props "warn_tdd") then
                Fable.Core.JS.console.warn (
                    $"Schema soft warning: FileMutation tool '{toolName}' is missing field 'warn_tdd' in properties"
                )

            if List.contains ProcessExecution caps && Dyn.isNullish (Dyn.get props "warn") then
                Fable.Core.JS.console.warn (
                    $"Schema soft warning: ProcessExecution tool '{toolName}' is missing field 'warn' in properties"
                )

            if
                List.contains SubagentDelegation caps
                && Dyn.isNullish (Dyn.get props "warn_reuse")
            then
                Fable.Core.JS.console.warn (
                    $"Schema soft warning: SubagentDelegation tool '{toolName}' is missing field 'warn_reuse' in properties"
                )

            schema

/// Replace prior registrations for this tool, then register types from schema.
let registerSchemaTypes (toolName: string) (schema: obj) : unit =
    if toolName <> "" then
        removeRegistryForTool toolName

        let decorated = decorateAndValidateSchema toolName schema

        extractSchemaTypes decorated
        |> List.map (fun (field, st) -> (toolName, field, st))
        |> registerToolParameterTypes


let private canonicalToolName (toolName: string) : string =
    match toolName.ToLowerInvariant() with
    | "file_read"
    | "read" -> "read"
    | "file_write"
    | "write" -> "write"
    | "task"
    | "todowrite" -> "todowrite"
    | other -> other

let private tryCoerceStringValue (expected: SchemaType) (s: string) : obj option =
    let trimmed = s.Trim()

    match expected with
    | SString -> None
    | SNumber ->
        match System.Double.TryParse trimmed with
        | true, d -> Some(box d)
        | _ -> None
    | SBoolean ->
        match trimmed.ToLowerInvariant() with
        | "true" -> Some(box true)
        | "false" -> Some(box false)
        | _ -> None
    | SObject ->
        match Decode.Auto.fromString<obj> trimmed with
        | Ok parsed ->
            if
                not (Dyn.isNullish parsed)
                && Dyn.typeIs parsed "object"
                && not (Dyn.isArray parsed)
            then
                Some parsed
            else
                None
        | Error _ -> None
    | SArray ->
        match Decode.Auto.fromString<obj> trimmed with
        | Ok parsed -> if Dyn.isArray parsed then Some parsed else None
        | Error _ -> None

/// Pre-process tool args: coerce string-encoded numbers to numbers,
/// parse JSON-stringified arrays/objects, etc.
let coerceArgsTypes (toolName: string) (args: obj) : unit =
    if not (Dyn.isNullish args) && Dyn.typeIs args "object" then
        let cleanTool = canonicalToolName toolName

        for k in Dyn.keys args do
            let v = Dyn.get args k

            if not (Dyn.isNullish v) && Dyn.typeIs v "string" then
                let s = unbox<string> v

                match parseSchemaToTypes cleanTool k with
                | Some expected ->
                    match tryCoerceStringValue expected s with
                    | Some coerced -> args?(k) <- coerced
                    | None -> ()
                | None -> ()

let private registerCoreToolSchemas () : unit =
    registerToolParameterTypes
        [ ("read", "offset", SNumber)
          ("read", "limit", SNumber)
          ("websearch", "numResults", SNumber)
          ("webfetch", "timeout", SNumber)
          ("coder", "intents", SArray)
          ("investigator", "intents", SArray)
          ("executor", "dependencies", SObject)
          ("todowrite", "todos", SArray)
          ("todowrite", "select_methodology", SArray)
          ("submit_review", "affectedFiles", SArray) ]

do registerCoreToolSchemas ()

let executeBeforeGateway (tool: string) (args: obj) : Result<obj * ControlEnvelope, string> =
    if Dyn.isNullish args then
        let empty = createObj []

        Result.Ok(
            empty,
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

        // Extract control fields from the original args object (in-place)
        let warnTddVal =
            match DynField.strField args "warn_tdd" with
            | Some v ->
                Dyn.deleteKey args "warn_tdd"
                Some v
            | None -> None

        let warnVal =
            match DynField.strField args "warn" with
            | Some v ->
                Dyn.deleteKey args "warn"
                Some v
            | None -> None

        let warnReuseVal =
            match DynField.strField args "warn_reuse" with
            | Some v ->
                Dyn.deleteKey args "warn_reuse"
                Some v
            | None -> None


        let hasControlFields = warnTddVal.IsSome || warnVal.IsSome || warnReuseVal.IsSome

        // Shallow clone the purified args to nextArgs if needed
        let nextArgs =
            if not caps.IsEmpty || hasControlFields then
                Dyn.cloneShallow args
            else
                args

        // Validate soft required fields into violations list
        let violations =
            [ if List.contains FileMutation caps then
                  match warnTddVal with
                  | None -> yield "warn_tdd: missing required acknowledgement"
                  | Some v ->
                      let norm = v.Trim().ToLowerInvariant()

                      if norm <> WarnTdd.canonicalValue then
                          yield
                              $"warn_tdd: value is invalid (got '%s{v}', expected canonical acknowledgement '%s{WarnTdd.canonicalValue}')"

              if List.contains ProcessExecution caps then
                  match warnVal with
                  | None -> yield "warn: missing required acknowledgement"
                  | Some v ->
                      let norm = v.Trim().ToLowerInvariant()

                      if norm <> WarnTdd.warnCanonicalValue then
                          yield
                              $"warn: value is invalid (got '%s{v}', expected canonical acknowledgement '%s{WarnTdd.warnCanonicalValue}')"

              if List.contains SubagentDelegation caps then
                  match warnReuseVal with
                  | None -> yield "warn_reuse: missing required acknowledgement"
                  | Some v ->
                      let norm = v.Trim().ToLowerInvariant()

                      if norm <> WarnTdd.warnReuseCanonicalValue then
                          yield
                              $"warn_reuse: value is invalid (got '%s{v}', expected canonical acknowledgement '%s{WarnTdd.warnReuseCanonicalValue}')" ]


        let env =
            { WarnTdd = warnTddVal
              Warn = warnVal
              WarnReuse = warnReuseVal
              Violations = violations
              GenerationAtStart = 0
              SessionId = "" }

        Result.Ok(nextArgs, env)
