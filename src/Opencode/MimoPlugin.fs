module VibeFs.Opencode.MimoPlugin

open Fable.Core
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.Plugin

[<ExportDefault>]
let plugin (ctx: obj) = pluginFor mimocode ctx
