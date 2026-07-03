module Wanxiangshu.Mux.PluginRegistrationParts

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Mux.WrappersReview
open Wanxiangshu.Mux.EventHook
open Wanxiangshu.Mux.SlashCommands
open Wanxiangshu.Mux.MessageTransform
open Wanxiangshu.Mux.PluginCatalog
open Wanxiangshu.Mux.BacklogSession
open Wanxiangshu.Shell.RuntimeScope

let createWrapperExecution (toolsObj: obj) (hostReadExec: HostFunctionCapture) (scope: RuntimeScope) : obj =
    createAllWrappers toolsObj hostReadExec scope

let createMessageTransforms
    (deps: obj)
    (scope: RuntimeScope)
    (backlogSession: BacklogSession)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    : obj =
    let messagesTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            messagesTransform deps scope backlogSession reviewStore input output)
    box messagesTransformFn

let createEventHooksSlashAndPolicy
    (deps: obj)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    : obj * obj * obj =
    let eventHook = createEventHook deps (fun sid -> reviewStore.deactivateReview sid)
    let slashCommands = createSlashCommands deps muxToolNames reviewStore
    let getToolPolicy = System.Func<string, obj, obj>(fun (_agentId: string) (role: obj) -> buildToolPolicy muxToolNames role)
    (box eventHook, box slashCommands, box getToolPolicy)

let createReviewTestSurface (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) : obj =
    createObj
        [ "activateReview",
          box (System.Func<string, string, int64, unit>(fun sessionID task createdAt ->
              reviewStore.activateReview(sessionID, task, createdAt)))
          "deactivateReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.deactivateReview sessionID))
          "getReviewTask", box (System.Func<string, string option>(fun sessionID -> reviewStore.getReviewTask sessionID))
          "tryLockReview", box (System.Func<string, bool>(fun sessionID -> reviewStore.tryLockReview sessionID))
          "unlockReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.unlockReview sessionID)) ]

let assembleRegistrationObject
    (scope: RuntimeScope)
    (tools: ToolDefinition array)
    (wrappers: obj)
    (mcpServers: obj)
    (eventHook: obj)
    (slashCommands: obj)
    (messagesTransform: obj)
    (getToolPolicy: obj)
    (reviewTestSurface: obj)
    : obj =
    createObj [
        "__runtimeScope", box scope
        "toolNames", box muxToolNames
        "tools", box tools
        "wrappers", box wrappers
        "mcpServers", box mcpServers
        "eventHook", box eventHook
        "slashCommands", box slashCommands
        "messagesTransform", box messagesTransform
        "getToolPolicy", box getToolPolicy
        "__reviewStore", box reviewTestSurface
        "tool.execute.after", box (System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            Wanxiangshu.Mux.PluginCatalog.toolExecuteAfter input output)) ]
