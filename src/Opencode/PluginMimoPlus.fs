module VibeFs.Opencode.PluginMimoPlus

open Fable.Core
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.PluginCore

[<ExportDefault>]
let plugin (ctx: obj) = pluginFor mimocode true ctx
