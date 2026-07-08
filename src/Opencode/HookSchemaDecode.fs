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
open Wanxiangshu.Opencode.HookSchemaZod

let private inlineJsonWarnTddProperty =
    Wanxiangshu.Opencode.HookSchemaCore.inlineJsonWarnTddProperty

let private inlineJsonWarnProperty =
    Wanxiangshu.Opencode.HookSchemaCore.inlineJsonWarnProperty

let private tryBuildJsonSchemaFromEffectSchema (parameters: obj) : obj =
    Wanxiangshu.Opencode.HookSchemaCore.tryBuildJsonSchemaFromEffectSchema parameters

let private appendRequiredWarnTddInPlace (schema: obj) : unit =
    let existingRequired = get schema "required"

    if isArray existingRequired then
        let arr = unbox<obj[]> existingRequired

        if not (arr |> Array.exists (fun x -> string x = "warn_tdd")) then
            existingRequired?("push") (box "warn_tdd") |> ignore
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
    if isNullish schema then
        schema
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
            existingRequired?("push") (box "warn") |> ignore
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
    if isNullish schema then
        schema
    else
        let props = get schema "properties"

        if not (isNullish props) then
            injectWarnIntoJsonSchemaInPlace schema
        else
            injectWarnIntoArgsShapeInPlace schema

        schema

let private stringZodProperty (description: string) : obj =
    createObj [ "type", box "string"; "minLength", box 1; "description", box description ]

let private jsonStringMinLengthProperty (minLength: int) (description: string) : obj =
    createObj
        [ "type", box "string"
          "minLength", box minLength
          "description", box description ]

let private reportFieldDescs =
    [| "ahaMoments", ahaMomentsDesc
       "changesAndReasons", changesAndReasonsDesc
       "gotchas", gotchasDesc
       "lessonsAndConventions", lessonsAndConventionsDesc
       "plan", planDesc |]
    |> Map.ofArray

let mergeWorkBacklogReportIntoTaskSchema (schema: obj) : obj =
    if isNullish schema then
        schema
    elif
        hasSchemaMethod (unbox<IZodSchema> schema) "safeExtend"
        || hasSchemaMethod (unbox<IZodSchema> schema) "extend"
    then
        extendZodTaskSchema (unbox<IZodSchema> schema)
    else
        let properties = get schema "properties"

        if isNullish properties then
            schema
        else
            [| "ahaMoments"
               "changesAndReasons"
               "gotchas"
               "lessonsAndConventions"
               "plan" |]
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
                    createObj
                        [ for key in Dyn.keys schema do
                              if key = "properties" then
                                  yield key, nextProperties
                              elif key = "required" then
                                  yield
                                      key,
                                      requiredWithoutTaskId (get schema "required")
                                      |> appendRequiredKey "select_methodology"
                              else
                                  yield key, get schema key ]
            else
                createObj
                    [ for key in Dyn.keys schema do
                          if key = "required" then
                              yield key, appendRequiredKey "select_methodology" (get schema "required")
                          else
                              yield key, get schema key ]
