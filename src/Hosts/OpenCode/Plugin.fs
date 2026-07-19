module Wanxiangshu.Hosts.Opencode.Plugin

open Fable.Core
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Opencode.PluginComposition

/// The opencode plugin entry point. This file is loaded directly by the host;
/// its compiled module must export exactly one factory. All
/// shared builder logic lives in PluginComposition so it is never re-exported here.
open Fable.Core.JsInterop

let plugin (ctx: obj) : JS.Promise<obj> = pluginFor opencode ctx

let pluginForWithSeams (ctx: obj) =
    Wanxiangshu.Hosts.Opencode.PluginComposition.pluginForWithSeams opencode ctx

[<ExportDefault>]
let defaultExport =
    createObj
        [ "id", box "wanxiangshu"
          "server", box (fun (ctx: obj) -> plugin ctx)
          "setup", box (fun (ctx: obj) -> plugin ctx)
          "pluginForWithSeams", box (fun (ctx: obj) -> pluginForWithSeams ctx) ]
