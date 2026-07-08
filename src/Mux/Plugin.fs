module Wanxiangshu.Mux.Plugin

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Mux.PluginCatalog
open Wanxiangshu.Shell.WorkspaceFiles

module Dyn = Wanxiangshu.Shell.Dyn

let muxToolNames = PluginCatalog.muxToolNames

let getPluginToolPolicy (agentId: string) (role: obj) : obj = buildToolPolicy muxToolNames role

let buildCapsFileReadData = Wanxiangshu.Shell.CapsFileCache.buildCapsFileReadData

let createToolCatalog deps toolNames reviewStore hostReadExec finderCache sessionScope =
    PluginCatalog.createToolCatalog deps toolNames reviewStore hostReadExec finderCache sessionScope

let createRegistration deps =
    Wanxiangshu.Mux.PluginRegistration.createRegistration deps
