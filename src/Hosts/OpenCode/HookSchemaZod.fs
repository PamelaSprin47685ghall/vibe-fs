module Wanxiangshu.Hosts.Opencode.HookSchemaZod

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Runtime.WorkBacklogSchema
open Wanxiangshu.Hosts.Opencode.HookSchemaDecoration

type IZodDef =
    abstract typeName: string

type IZodSchema =
    abstract _def: IZodDef
    abstract shape: obj
    abstract extend: obj
    abstract safeExtend: obj
    abstract unwrap: obj
    abstract describe: obj
    abstract array: obj
    abstract min: obj
    abstract optional: obj

let hasSchemaMethod (schema: IZodSchema) (methodName: string) : bool =
    let f =
        match methodName with
        | "safeExtend" -> schema.safeExtend
        | "extend" -> schema.extend
        | "unwrap" -> schema.unwrap
        | "describe" -> schema.describe
        | "array" -> schema.array
        | "min" -> schema.min
        | "optional" -> schema.optional
        | _ -> null

    not (Dyn.isNullish f) && jsType f = "function"

let tryCallSchemaMethod0 (schema: IZodSchema) (methodName: string) : obj option =
    let f =
        match methodName with
        | "unwrap" -> schema.unwrap
        | "array" -> schema.array
        | "optional" -> schema.optional
        | _ -> null

    if not (Dyn.isNullish f) && jsType f = "function" then
        Some(f $ ())
    else
        None

let tryCallSchemaMethod (schema: IZodSchema) (methodName: string) (arg: obj) : obj option =
    let f =
        match methodName with
        | "describe" -> schema.describe
        | "min" -> schema.min
        | "safeExtend" -> schema.safeExtend
        | "extend" -> schema.extend
        | _ -> null

    if not (Dyn.isNullish f) && jsType f = "function" then
        Some(f $ (arg))
    else
        None

let tryGetNestedTaskStringSchema (schema: IZodSchema) : IZodSchema option =
    try
        let shape = schema.shape

        if isNullish shape then
            None
        else
            let operation = get shape "operation"

            if isNullish operation then
                None
            else
                let options = get operation "options"

                if not (isArray options) then
                    None
                else
                    options :?> obj array
                    |> Array.tryPick (fun option ->
                        if isNullish option then
                            None
                        else
                            let optionZod = unbox<IZodSchema> option
                            let optionShape = optionZod.shape

                            if isNullish optionShape then
                                None
                            else
                                [| "summary"; "event_summary"; "id"; "session_id" |]
                                |> Array.tryPick (fun key ->
                                    let candidate = get optionShape key

                                    if isNullish candidate then
                                        None
                                    else
                                        Some(unbox<IZodSchema> candidate)))
    with _ ->
        None

let scanZodShape (shape: obj) (schema: IZodSchema) : IZodSchema option * Set<string> * IZodSchema option =
    let keys = Dyn.keys shape
    let mutable templateStr: IZodSchema option = None
    let mutable existingReportFields: Set<string> = Set.empty
    let mutable existingMethodology: IZodSchema option = None

    for k in keys do
        let prop = unbox<IZodSchema> (get shape k)

        if Wanxiangshu.Kernel.WorkBacklog.reportFieldNames |> List.contains k then
            existingReportFields <- Set.add k existingReportFields

        if k = "select_methodology" then
            existingMethodology <- Some prop

        let def = prop._def

        if not (isNullish def) then
            let typeName = def.typeName

            if not (isNullish typeName) && string typeName = "ZodString" then
                templateStr <- Some prop

    if templateStr.IsNone then
        match tryGetNestedTaskStringSchema schema with
        | Some candidate ->
            match tryCallSchemaMethod0 candidate "unwrap" with
            | Some inner -> templateStr <- Some(unbox<IZodSchema> inner)
            | None -> templateStr <- Some candidate
        | None -> ()

    (templateStr, existingReportFields, existingMethodology)

let buildExtensionProperties
    (templateStr: IZodSchema)
    (existingReportFields: Set<string>)
    (existingMethodology: IZodSchema option)
    : (string * obj) list =
    let mutable extProps: (string * obj) list = []

    let reportFields = Wanxiangshu.Kernel.WorkBacklog.reportFieldNames

    for field in reportFields do
        let desc =
            match field with
            | "ahaMoments" -> ahaMomentsDesc
            | "changesAndReasons" -> changesAndReasonsDesc
            | "gotchas" -> gotchasDesc
            | "lessonsAndConventions" -> lessonsAndConventionsDesc
            | "plan" -> planDesc
            | _ -> ""

        match tryCallSchemaMethod templateStr "describe" (box desc) with
        | Some describedStr ->
            match tryCallSchemaMethod0 (unbox<IZodSchema> describedStr) "optional" with
            | Some optionalStr -> extProps <- (field, optionalStr) :: extProps
            | None -> ()
        | None -> ()

    if existingMethodology.IsNone then
        match tryCallSchemaMethod0 templateStr "array" with
        | Some arr ->
            let arrZod = unbox<IZodSchema> arr

            match tryCallSchemaMethod arrZod "min" (box 1) with
            | Some minArr ->
                let minArrZod = unbox<IZodSchema> minArr

                match
                    tryCallSchemaMethod
                        minArrZod
                        "describe"
                        (box Wanxiangshu.Kernel.Methodology.selectMethodologyFieldDescription)
                with
                | Some descArr -> extProps <- ("select_methodology", descArr) :: extProps
                | None -> ()
            | None -> ()
        | None -> ()

    extProps

let extendZodTaskSchema (schema: IZodSchema) : obj =
    try
        let shape = schema.shape

        if isNullish shape then
            box schema
        else
            let templateStr, existingReportFields, existingMethodology =
                scanZodShape shape schema

            match templateStr with
            | None -> box schema
            | Some ts ->
                let extProps = buildExtensionProperties ts existingReportFields existingMethodology

                if List.isEmpty extProps then
                    box schema
                else
                    let extension = createObj extProps

                    match tryCallSchemaMethod schema "safeExtend" extension with
                    | Some next -> next
                    | None ->
                        match tryCallSchemaMethod schema "extend" extension with
                        | Some next -> next
                        | None -> box schema
    with _ ->
        box schema
