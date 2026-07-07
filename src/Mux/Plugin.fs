module Wanxiangshu.Mux.Plugin

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Mux.PluginCatalog
open Wanxiangshu.Mux.ReadDedup
open Wanxiangshu.Shell.WorkspaceFiles
module Dyn = Wanxiangshu.Shell.Dyn

let muxToolNames = PluginCatalog.muxToolNames

let getPluginToolPolicy (agentId: string) (role: obj) : obj =
    buildToolPolicy muxToolNames role

let collectReadOutputs (messages: obj array) : string[] =
    Wanxiangshu.Shell.ReadDedupMuxPlugin.collectReadOutputs messages

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj[] =
    Wanxiangshu.Shell.ReadDedupMuxPlugin.deduplicateReadOutputsWithSeen seenOutputs messages

let deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj[] =
    ReadDedup.deduplicateModelReadOutputsWithSeen seenOutputs messages

let buildCapsFileReadData = Wanxiangshu.Shell.CapsFileCache.buildCapsFileReadData

let createToolCatalog deps toolNames reviewStore hostReadExec finderCache sessionScope =
    PluginCatalog.createToolCatalog deps toolNames reviewStore hostReadExec finderCache sessionScope

let createRegistration deps =
    Wanxiangshu.Mux.PluginRegistration.createRegistration deps