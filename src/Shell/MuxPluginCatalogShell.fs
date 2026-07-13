module Wanxiangshu.Shell.MuxPluginCatalogShell

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.MuxToolDefinition


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
                           "enum", box [| box WarnTdd.canonicalValue |]
                           "description", box WarnTdd.warnTddDescription
                           "x-wanxiangshu-soft-required", box true |]
                )
        else
            props?("warn_tdd")?("x-wanxiangshu-soft-required") <- true

    tool

let injectWarnIntoMuxSchema (tool: ToolDefinition) : ToolDefinition =
    if WarnTdd.isWarnRequiredTool tool.name then
        let props = tool.parameters.properties

        if isNullish (props?warn) then
            props?("warn") <-
                box (
                    createObj
                        [| "type", box "string"
                           "enum", box [| box WarnTdd.warnCanonicalValue |]
                           "description", box WarnTdd.warnDescription
                           "x-wanxiangshu-soft-required", box true |]
                )
        else
            props?("warn")?("x-wanxiangshu-soft-required") <- true

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
                           "enum", box [| box WarnTdd.warnReuseCanonicalValue |]
                           "description", box WarnTdd.warnReuseDescription
                           "x-wanxiangshu-soft-required", box true |]
                )
        else
            props?("warn_reuse")?("x-wanxiangshu-soft-required") <- true

    tool
