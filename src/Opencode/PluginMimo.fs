module VibeFs.Opencode.PluginMimo

open Fable.Core
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.PluginCore

[<ExportDefault>]
let plugin (ctx: obj) = pluginFor mimocode ctx
