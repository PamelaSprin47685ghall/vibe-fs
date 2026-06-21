module VibeFs.Opencode.PluginPlus

open Fable.Core
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.PluginCore

[<ExportDefault>]
let plugin (ctx: obj) : JS.Promise<obj> = pluginFor opencode true ctx
