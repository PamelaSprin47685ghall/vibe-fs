module Wanxiangshu.Opencode.PluginMimo

open Fable.Core
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Opencode.PluginCore

open Fable.Core.JsInterop

let plugin (ctx: obj) : JS.Promise<obj> = pluginFor mimocode ctx

[<ExportDefault>]
let defaultExport =
    createObj
        [ "id", box "wanxiangshu"
          "server", box (fun (ctx: obj) -> plugin ctx)
          "setup", box (fun (ctx: obj) -> plugin ctx) ]
