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

let private appendRequiredWarnTddInPlace (schema: obj) : unit = ()

let private removeRequiredKey (schema: obj) (key: string) : unit =
    if not (isNullish schema) then
        let existingRequired = get schema "required"

        if isArray existingRequired then
            let arr = unbox<obj[]> existingRequired
            let nextReq = arr |> Array.filter (fun x -> string x <> key)
            schema?("required") <- box nextReq

let private injectWarnTddIntoJsonSchemaInPlace (schema: obj) : unit =
    let props = get schema "properties"

    if not (isNullish props) then
        if isNullish (get props "warn_tdd") then
            props?("warn_tdd") <- inlineJsonWarnTddProperty
        else
            let prop = get props "warn_tdd"

            if not (isNullish prop) then
                prop?("required_") <- true

                if Dyn.str prop "description" = "" then
                    prop?("description") <- box Params.warnTddDesc

        appendRequiredWarnTddInPlace schema

let private injectWarnTddIntoArgsShapeInPlace (shape: obj) : unit =
    shape?("warn_tdd") <- strOpt Params.warnTddDesc

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

let private appendRequiredWarnInPlace (schema: obj) : unit = ()

let private injectWarnIntoJsonSchemaInPlace (schema: obj) : unit =
    let props = get schema "properties"

    if not (isNullish props) then
        if isNullish (get props "warn") then
            props?("warn") <- inlineJsonWarnProperty
        else
            let prop = get props "warn"

            if not (isNullish prop) then
                Dyn.setKey prop "required_" true

        appendRequiredWarnInPlace schema

let private injectWarnIntoArgsShapeInPlace (shape: obj) : unit =
    shape?("warn") <- strOpt WarnTdd.warnDescription

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

let private reportFieldDescs =
    [| "ahaMoments", ahaMomentsDesc
       "changesAndReasons", changesAndReasonsDesc
       "gotchas", gotchasDesc
       "lessonsAndConventions", lessonsAndConventionsDesc
       "plan", planDesc |]
    |> Map.ofArray

let private inlineJsonWarnReuseProperty =
    Wanxiangshu.Opencode.HookSchemaCore.inlineJsonWarnReuseProperty

let private appendRequiredWarnReuseInPlace (schema: obj) : unit = ()

let private injectWarnReuseIntoJsonSchemaInPlace (schema: obj) : unit =
    let props = get schema "properties"

    if not (isNullish props) then
        if isNullish (get props "warn_reuse") then
            props?("warn_reuse") <- inlineJsonWarnReuseProperty
        else
            let prop = get props "warn_reuse"

            if not (isNullish prop) then
                Dyn.setKey prop "required_" true

        appendRequiredWarnReuseInPlace schema

let private injectWarnReuseIntoArgsShapeInPlace (shape: obj) : unit =
    shape?("warn_reuse") <- strOpt WarnTdd.warnReuseDescription

/// Inject warn_reuse into an Opencode tool schema in place.
let injectWarnReuseIntoJsonSchema (schema: obj) : obj =
    if isNullish schema then
        schema
    else
        let props = get schema "properties"

        if not (isNullish props) then
            injectWarnReuseIntoJsonSchemaInPlace schema
        else
            injectWarnReuseIntoArgsShapeInPlace schema

        schema

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
            let reportFields =
                [| "ahaMoments"
                   "changesAndReasons"
                   "gotchas"
                   "lessonsAndConventions"
                   "plan" |]

            reportFields
            |> Array.iter (fun field ->
                let existingProp = get properties field

                if isNullish existingProp then
                    properties?(field) <- WorkBacklogSchema.jsonStringMinLengthProperty 1024 (reportFieldDescs.[field])
                else
                    if isNullish (get existingProp "minLength") then
                        existingProp?("minLength") <- box 1024

                    let currentDesc = Dyn.str existingProp "description"

                    let cleanDesc =
                        if currentDesc.Contains("MUST be at least") then
                            currentDesc
                        else
                            "MUST be at least 1024 characters. "
                            + currentDesc
                            + " "
                            + reportFieldDescs.[field]

                    existingProp?("description") <- box (cleanDesc.Trim()))

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
                                  let req = get schema "required"
                                  yield key, requiredWithoutTaskId req |> appendRequiredKey "select_methodology"
                              else
                                  yield key, get schema key ]
            else
                createObj
                    [ for key in Dyn.keys schema do
                          if key = "required" then
                              let req = get schema "required"
                              yield key, appendRequiredKey "select_methodology" req
                          else
                              yield key, get schema key ]
