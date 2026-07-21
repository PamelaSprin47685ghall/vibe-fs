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
