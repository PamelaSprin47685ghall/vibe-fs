module Wanxiangshu.Runtime.MuxPluginCatalogShell

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.MuxToolDefinition


[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let envVar (name: string) : string =
    let v = nodeProcess?env?(name)
    if isNull v then "" else string v

let setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v

let toolsToObject (tools: ToolDefinition array) : obj =
    createObj [ for t in tools -> t.name, box t ]

let addRequired (schema: obj) (key: string) : unit =
    let existing = schema?required

    if Dyn.isArray existing then
        existing?("push") (box key) |> ignore
    else
        schema?("required") <- box [| box key |]

let injectWarnTddIntoMuxSchema (tool: ToolDefinition) : ToolDefinition =
    if WarnTdd.isModificationTool tool.name then
        let props = tool.parameters.properties

        if isNullish (props?warn_tdd) then
            props?("warn_tdd") <-
                box (
                    createObj
                        [| "type", box "string"
                           "description", box WarnTdd.warnTddDescription
                           "required_", box true |]
                )
        else
            let prop = props?warn_tdd

            if not (isNullish prop) then
                prop?("required_") <- true

    tool

let injectWarnIntoMuxSchema (tool: ToolDefinition) : ToolDefinition =
    if WarnTdd.isWarnRequiredTool tool.name then
        let props = tool.parameters.properties

        if isNullish (props?warn) then
            props?("warn") <-
                box (
                    createObj
                        [| "type", box "string"
                           "description", box WarnTdd.warnDescription
                           "required_", box true |]
                )
        else
            let prop = props?warn

            if not (isNullish prop) then
                prop?("required_") <- true

    tool

let injectWarnWarnTddIntoMuxSchema (tool: ToolDefinition) : ToolDefinition =
    injectWarnTddIntoMuxSchema (injectWarnIntoMuxSchema tool)


let injectWarnReuseIntoMuxSchema (tool: ToolDefinition) : ToolDefinition =
    if WarnTdd.isSubagentTool tool.name then
        let props = tool.parameters.properties

        if isNullish (props?warn_reuse) then
            props?("warn_reuse") <-
                box (
                    createObj
                        [| "type", box "string"
                           "description", box WarnTdd.warnReuseDescription
                           "required_", box true |]
                )
        else
            let prop = props?warn_reuse

            if not (isNullish prop) then
                prop?("required_") <- true

    tool
