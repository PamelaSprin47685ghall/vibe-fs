module Wanxiangshu.Opencode.HookSchemaDecode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Shell.SubagentIntentsCodec
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Opencode.ToolSchema
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.WorkBacklogSchema
open Wanxiangshu.Opencode.HookSchemaCore

let private inlineJsonWarnTddProperty = Wanxiangshu.Opencode.HookSchemaCore.inlineJsonWarnTddProperty
let private inlineJsonWarnProperty = Wanxiangshu.Opencode.HookSchemaCore.inlineJsonWarnProperty
let private tryBuildJsonSchemaFromEffectSchema (parameters: obj) : obj = Wanxiangshu.Opencode.HookSchemaCore.tryBuildJsonSchemaFromEffectSchema parameters

let private appendRequiredWarnTddInPlace (schema: obj) : unit =
    let existingRequired = get schema "required"
    if isArray existingRequired then
        let arr = unbox<obj[]> existingRequired
        if not (arr |> Array.exists (fun x -> string x = "warn_tdd")) then
            existingRequired?("push")(box "warn_tdd") |> ignore
    else
        schema?("required") <- box [| box "warn_tdd" |]

let private injectWarnTddIntoJsonSchemaInPlace (schema: obj) : unit =
    let props = get schema "properties"
    if not (isNullish props) then
        if isNullish (get props "warn_tdd") then
            props?("warn_tdd") <- inlineJsonWarnTddProperty
        appendRequiredWarnTddInPlace schema

let private injectWarnTddIntoArgsShapeInPlace (shape: obj) : unit =
    if isNullish (get shape "warn_tdd") then
        shape?("warn_tdd") <- enumReq [| WarnTdd.canonicalValue |] Params.warnTddDesc

/// Inject warn_tdd into an Opencode tool schema in place.
let injectWarnTddIntoJsonSchema (schema: obj) : obj =
    if isNullish schema then schema
    else
        let props = get schema "properties"
        if not (isNullish props) then
            injectWarnTddIntoJsonSchemaInPlace schema
        else
            injectWarnTddIntoArgsShapeInPlace schema
        schema

let private appendRequiredWarnInPlace (schema: obj) : unit =
    let existingRequired = get schema "required"
    if isArray existingRequired then
        let arr = unbox<obj[]> existingRequired
        if not (arr |> Array.exists (fun x -> string x = "warn")) then
            existingRequired?("push")(box "warn") |> ignore
    else
        schema?("required") <- box [| box "warn" |]

let private injectWarnIntoJsonSchemaInPlace (schema: obj) : unit =
    let props = get schema "properties"
    if not (isNullish props) then
        if isNullish (get props "warn") then
            props?("warn") <- inlineJsonWarnProperty
        appendRequiredWarnInPlace schema

let private injectWarnIntoArgsShapeInPlace (shape: obj) : unit =
    if isNullish (get shape "warn") then
        shape?("warn") <- enumReq [| WarnTdd.warnCanonicalValue |] WarnTdd.warnDescription

/// Inject warn into an Opencode tool schema in place.
let injectWarnIntoJsonSchema (schema: obj) : obj =
    if isNullish schema then schema
    else
        let props = get schema "properties"
        if not (isNullish props) then
            injectWarnIntoJsonSchemaInPlace schema
        else
            injectWarnIntoArgsShapeInPlace schema
        schema

let private stringZodProperty (description: string) : obj =
    createObj [
        "type", box "string"
        "minLength", box 1
        "description", box description
    ]

let private jsonStringMinLengthProperty (minLength: int) (description: string) : obj =
    createObj [ "type", box "string"; "minLength", box minLength; "description", box description ]

let private reportFieldDescs =
    [| "ahaMoments", ahaMomentsDesc
       "changesAndReasons", changesAndReasonsDesc
       "gotchas", gotchasDesc
       "lessonsAndConventions", lessonsAndConventionsDesc
       "plan", planDesc |]
    |> Map.ofArray

let private hasCallable (o: obj) (key: string) : bool =
    let value = get o key
    not (isNullish value) && jsType value = "function"

let private callSchemaMethodOpt (schema: obj) (methodName: string) (arg: obj option) : obj option =
    if hasCallable schema methodName then
        match arg with
        | Some a -> Some (ToolSchema.call1 schema methodName a)
        | None -> Some (ToolSchema.call0 schema methodName)
    else None

let private callSchemaMethod (schema: obj) (methodName: string) (arg: obj) : obj option =
    callSchemaMethodOpt schema methodName (Some arg)

let private callSchemaMethod0 (schema: obj) (methodName: string) : obj option =
    callSchemaMethodOpt schema methodName None

let private tryGetNestedTaskStringSchema (schema: obj) : obj option =
    try
        let shape = get schema "shape"
        if isNullish shape then None
        else
            let operation = get shape "operation"
            let options = get operation "options"
            if not (isArray options) then None
            else
                options :?> obj array
                |> Array.tryPick (fun optionSchema ->
                    let optionShape = get optionSchema "shape"
                    if isNullish optionShape then None
                    else
                        [| "summary"; "event_summary"; "id"; "session_id" |]
                        |> Array.tryPick (fun key ->
                            let candidate = get optionShape key
                            if isNullish candidate then None else Some candidate))
    with _ -> None

let private extendZodTaskSchema (schema: obj) : obj =
    try
        let shape = get schema "shape"
        if isNullish shape then schema
        else
            let keys = Dyn.keys shape
            let mutable templateStr = null
            let mutable existingReportFields : Set<string> = Set.empty
            let mutable existingMethodology = null
            for k in keys do
                let prop = get shape k
                if Wanxiangshu.Kernel.WorkBacklog.reportFieldNames |> List.contains k then existingReportFields <- Set.add k existingReportFields
                if k = "select_methodology" then existingMethodology <- prop
                let typeName = get (get prop "_def") "typeName"
                if typeName = "ZodString" then
                    templateStr <- prop
            if isNullish templateStr then
                match tryGetNestedTaskStringSchema schema with
                | Some candidate ->
                    templateStr <-
                        match callSchemaMethod0 candidate "unwrap" with
                        | Some inner -> inner
                        | None -> candidate
                | None -> ()
            if isNullish templateStr then schema
            else
                let mutable extProps : (string * obj) list = []
                let missingReportFields = Wanxiangshu.Kernel.WorkBacklog.reportFieldNames |> List.filter (fun f -> not (Set.contains f existingReportFields))
                for field in missingReportFields do
                    let desc =
                        match field with
                        | "ahaMoments" -> ahaMomentsDesc
                        | "changesAndReasons" -> changesAndReasonsDesc
                        | "gotchas" -> gotchasDesc
                        | "lessonsAndConventions" -> lessonsAndConventionsDesc
                        | "plan" -> planDesc
                        | _ -> ""
                    match callSchemaMethod templateStr "describe" (box desc) with
                    | Some describedStr -> extProps <- (field, describedStr) :: extProps
                    | None -> ()
                if isNullish existingMethodology then
                    match callSchemaMethod0 templateStr "array" with
                    | Some arr ->
                        match callSchemaMethod arr "min" (box 1) with
                        | Some minArr ->
                            match callSchemaMethod minArr "describe" (box Wanxiangshu.Kernel.Methodology.selectMethodologyFieldDescription) with
                            | Some descArr -> extProps <- ("select_methodology", descArr) :: extProps
                            | None -> ()
                        | None -> ()
                    | None -> ()
                if List.isEmpty extProps then schema
                else
                    let extension = createObj extProps
                    match callSchemaMethod schema "safeExtend" extension with
                    | Some next -> next
                    | None ->
                        match callSchemaMethod schema "extend" extension with
                        | Some next -> next
                        | None -> schema
    with _ -> schema

let mergeWorkBacklogReportIntoTaskSchema (schema: obj) : obj =
    if isNullish schema then schema
    elif hasCallable schema "safeExtend" || hasCallable schema "extend" then
        extendZodTaskSchema schema
    else
        let properties = get schema "properties"
        if isNullish properties then schema
        else
            [| "ahaMoments"; "changesAndReasons"; "gotchas"; "lessonsAndConventions"; "plan" |]
            |> Array.iter (fun field ->
                if isNullish (get properties field) then
                    properties?(field) <- jsonStringMinLengthProperty 1024 (reportFieldDescs.[field]))
            if isNullish (get properties "select_methodology") then
                properties?("select_methodology") <- selectMethodologyProperty
            if not (isNullish (get properties "task_id")) then
                Dyn.keys properties
                |> Array.filter (fun key -> key <> "task_id")
                |> Array.map (fun key -> key, get properties key)
                |> createObj
                |> fun nextProperties ->
                    createObj [ for key in Dyn.keys schema do if key = "properties" then yield key, nextProperties elif key = "required" then yield key, requiredWithoutTaskId (get schema "required") |> appendRequiredKey "select_methodology" else yield key, get schema key ]
            else
                createObj [ for key in Dyn.keys schema do if key = "required" then yield key, appendRequiredKey "select_methodology" (get schema "required") else yield key, get schema key ]
