module Wanxiangshu.Hosts.Mux.Plugin

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Mux.PluginCatalog
open Wanxiangshu.Runtime.WorkspaceFiles

module Dyn = Wanxiangshu.Runtime.Dyn

let muxToolNames = PluginCatalog.muxToolNames

let getPluginToolPolicy (agentId: string) (role: obj) : obj = buildToolPolicy muxToolNames role

let buildCapsFileReadData = Wanxiangshu.Runtime.CapsFileCache.buildCapsFileReadData

let createToolCatalog deps toolNames reviewStore hostReadExec finderCache sessionScope =
    PluginCatalog.createToolCatalog deps toolNames reviewStore hostReadExec finderCache sessionScope

let createRegistrationWithSeams deps =
    Wanxiangshu.Hosts.Mux.PluginRegistration.createRegistrationWithSeams deps

let createRegistration deps =
    Wanxiangshu.Hosts.Mux.PluginRegistration.createRegistration deps
