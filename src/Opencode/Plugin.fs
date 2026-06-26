module Wanxiangshu.Opencode.Plugin

open Fable.Core
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Opencode.PluginCore

/// The opencode plugin entry point. This file is loaded directly by the host;
/// its compiled module must export exactly one factory, because the host's
/// legacy loader invokes every exported function as a plugin factory. All
/// shared builder logic lives in PluginCore so it is never re-exported here.
[<ExportDefault>]
let plugin (ctx: obj) : JS.Promise<obj> = pluginFor opencode ctx
