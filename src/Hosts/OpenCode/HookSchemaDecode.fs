module Wanxiangshu.Hosts.Opencode.HookSchemaDecode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.SubagentIntentsCodec
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Opencode.HookSchemaDecoration

let private inlineJsonWarnTddProperty =
    Wanxiangshu.Hosts.Opencode.HookSchemaDecoration.inlineJsonWarnTddProperty

let private inlineJsonWarnProperty =
    Wanxiangshu.Hosts.Opencode.HookSchemaDecoration.inlineJsonWarnProperty

let private tryBuildJsonSchemaFromEffectSchema (parameters: obj) : obj =
    Wanxiangshu.Hosts.Opencode.HookSchemaDecoration.tryBuildJsonSchemaFromEffectSchema parameters

let private injectWarnTddIntoJsonSchemaInPlace (schema: obj) : unit =
    let props = get schema "properties"

    if not (isNullish props) then
        if isNullish (get props WarnTdd.warnTddKey) then
            props?(WarnTdd.warnTddKey) <- inlineJsonWarnTddProperty
        else
            let prop = get props WarnTdd.warnTddKey

            if not (isNullish prop) && Dyn.str prop "description" = "" then
                prop?("description") <- box Params.warnTddDesc

let private injectWarnTddIntoArgsShapeInPlace (shape: obj) : unit =
    shape?(WarnTdd.warnTddKey) <- inlineJsonWarnTddProperty

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

let private injectWarnIntoJsonSchemaInPlace (schema: obj) : unit =
    let props = get schema "properties"

    if not (isNullish props) then
        if isNullish (get props WarnTdd.warnKey) then
            props?(WarnTdd.warnKey) <- inlineJsonWarnProperty

let private injectWarnIntoArgsShapeInPlace (shape: obj) : unit =
    shape?(WarnTdd.warnKey) <- inlineJsonWarnProperty

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

let private inlineJsonWarnReuseProperty =
    Wanxiangshu.Hosts.Opencode.HookSchemaDecoration.inlineJsonWarnReuseProperty

let private injectWarnReuseIntoJsonSchemaInPlace (schema: obj) : unit =
    let props = get schema "properties"

    if not (isNullish props) then
        if isNullish (get props WarnTdd.warnReuseKey) then
            props?(WarnTdd.warnReuseKey) <- inlineJsonWarnReuseProperty

let private injectWarnReuseIntoArgsShapeInPlace (shape: obj) : unit =
    shape?(WarnTdd.warnReuseKey) <- inlineJsonWarnReuseProperty

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
