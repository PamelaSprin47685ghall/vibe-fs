module Wanxiangshu.Opencode.PluginMimo

open Fable.Core
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Opencode.PluginCore

[<ExportDefault>]
let plugin (ctx: obj) = pluginFor mimocode ctx
