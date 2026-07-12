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

        appendRequiredWarnTddInPlace schema

let private injectWarnTddIntoArgsShapeInPlace (shape: obj) : unit =
    if isNullish (get shape "warn_tdd") then
        shape?("warn_tdd") <- enumOpt [| WarnTdd.canonicalValue |] Params.warnTddDesc

/// Inject warn_tdd into an Opencode tool schema in place.
let injectWarnTddIntoJsonSchema (schema: obj) : obj =
    if isNullish schema then
        schema
    else
        removeRequiredKey schema "warn_tdd"
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

        appendRequiredWarnInPlace schema

let private injectWarnIntoArgsShapeInPlace (shape: obj) : unit =
    if isNullish (get shape "warn") then
        shape?("warn") <- enumOpt [| WarnTdd.warnCanonicalValue |] WarnTdd.warnDescription

/// Inject warn into an Opencode tool schema in place.
let injectWarnIntoJsonSchema (schema: obj) : obj =
    if isNullish schema then
        schema
    else
        removeRequiredKey schema "warn"
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
          "x-wanxiangshu-soft-min-length", box minLength
          "description", box ("MUST be at least " + string minLength + " characters. " + description) ]

let private reportFieldDescs =
    [| "ahaMoments", ahaMomentsDesc
       "changesAndReasons", changesAndReasonsDesc
       "gotchas", gotchasDesc
       "lessonsAndConventions", lessonsAndConventionsDesc
       "plan", planDesc |]
    |> Map.ofArray

let inlineJsonAmendProperty: obj =
    createObj
        [ "type", box "integer"
          "minimum", box 1
          "description",
          box
              "Undo/amend the last N tool call chains (including calls, results, and intermediate reasoning) by backtracking in history. The amend message itself is kept as a fresh starting point." ]

let private injectAmendIntoJsonSchemaInPlace (schema: obj) : unit =
    let props = get schema "properties"

    if not (isNullish props) then
        if isNullish (get props "amend") then
            props?("amend") <- inlineJsonAmendProperty

let private injectAmendIntoArgsShapeInPlace (shape: obj) : unit =
    if isNullish (get shape "amend") then
        shape?("amend") <-
            ToolSchema.numOpt
                "Undo/amend the last N tool call chains (including calls, results, and intermediate reasoning) by backtracking in history. The amend message itself is kept as a fresh starting point."

let private inlineJsonWarnReuseProperty =
    Wanxiangshu.Opencode.HookSchemaCore.inlineJsonWarnReuseProperty

let private appendRequiredWarnReuseInPlace (schema: obj) : unit = ()

let private injectWarnReuseIntoJsonSchemaInPlace (schema: obj) : unit =
    let props = get schema "properties"

    if not (isNullish props) then
        if isNullish (get props "warn_reuse") then
            props?("warn_reuse") <- inlineJsonWarnReuseProperty

        appendRequiredWarnReuseInPlace schema

let private injectWarnReuseIntoArgsShapeInPlace (shape: obj) : unit =
    if isNullish (get shape "warn_reuse") then
        shape?("warn_reuse") <- enumOpt [| WarnTdd.warnReuseCanonicalValue |] WarnTdd.warnReuseDescription

/// Inject warn_reuse into an Opencode tool schema in place.
let injectWarnReuseIntoJsonSchema (schema: obj) : obj =
    if isNullish schema then
        schema
    else
        removeRequiredKey schema "warn_reuse"
        let props = get schema "properties"

        if not (isNullish props) then
            injectWarnReuseIntoJsonSchemaInPlace schema
        else
            injectWarnReuseIntoArgsShapeInPlace schema

        schema

let injectAmendIntoJsonSchema (schema: obj) : obj =
    if isNullish schema then
        schema
    else
        let props = get schema "properties"

        if not (isNullish props) then
            injectAmendIntoJsonSchemaInPlace schema
        else
            injectAmendIntoArgsShapeInPlace schema

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

            let filterRequired (req: obj) =
                if isNullish req || not (Dyn.isArray req) then
                    req
                else
                    let arr = unbox<obj array> req

                    arr
                    |> Array.filter (fun k ->
                        let s = string k
                        not (Array.contains s reportFields))
                    |> box

            reportFields
            |> Array.iter (fun field ->
                let existingProp = get properties field

                if isNullish existingProp then
                    properties?(field) <- jsonStringMinLengthProperty 1024 (reportFieldDescs.[field])
                else
                    if not (isNullish (get existingProp "minLength")) then
                        Dyn.deleteKey existingProp "minLength"

                    existingProp?("x-wanxiangshu-soft-min-length") <- box 1024
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
                                  let filteredReq = filterRequired (get schema "required")
                                  yield key, requiredWithoutTaskId filteredReq |> appendRequiredKey "select_methodology"
                              else
                                  yield key, get schema key ]
            else
                createObj
                    [ for key in Dyn.keys schema do
                          if key = "required" then
                              let filteredReq = filterRequired (get schema "required")
                              yield key, appendRequiredKey "select_methodology" filteredReq
                          else
                              yield key, get schema key ]
