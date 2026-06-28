module Wanxiangshu.Mux.PluginRegistrationParts

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Shell
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Mux.WrappersReview
open Wanxiangshu.Mux.EventHook
open Wanxiangshu.Mux.SlashCommands
open Wanxiangshu.Mux.MessageTransform
open Wanxiangshu.Mux.PluginCatalog
open Wanxiangshu.Mux.BacklogSession
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.WorkspaceFiles

let createWrapperExecution (toolsObj: obj) (hostReadExec: HostFunctionCapture) (scope: RuntimeScope) : obj =
    createAllWrappers toolsObj hostReadExec scope

let createMessageTransforms
    (deps: obj)
    (scope: RuntimeScope)
    (backlogSession: BacklogSession)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    : obj * obj =
    let messagesTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            messagesTransform deps scope backlogSession reviewStore input output)
    let compactingTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            compactingTransform deps backlogSession input output)
    (box messagesTransformFn, box compactingTransformFn)

let createEventHooksSlashAndPolicy
    (deps: obj)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    : obj * obj * obj =
    let eventHook = createEventHook deps reviewStore
    let slashCommands = createSlashCommands deps muxToolNames reviewStore
    let getToolPolicy = System.Func<string, obj, obj>(fun (_agentId: string) (role: obj) -> buildToolPolicy muxToolNames role)
    (box eventHook, box slashCommands, box getToolPolicy)

let createContextInjector () : obj =
    box {| inject = (fun (projectPath: string) ->
        promise {
            let! files = findCapsFiles projectPath
            return if List.isEmpty files then box null else box (Wanxiangshu.Kernel.CapsFormat.buildCapitalsContext files)
        } :> obj) |}

let createReviewTestSurface (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) : obj =
    createObj
        [ "activateReview",
          box (System.Func<string, string, int64, unit>(fun sessionID task createdAt ->
              reviewStore.activateReview(sessionID, task, createdAt)))
          "deactivateReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.deactivateReview sessionID))
          "isReviewActive", box (System.Func<string, bool>(fun sessionID -> reviewStore.isReviewActive sessionID))
          "getReviewTask", box (System.Func<string, string option>(fun sessionID -> reviewStore.getReviewTask sessionID))
          "tryLockReview", box (System.Func<string, bool>(fun sessionID -> reviewStore.tryLockReview sessionID))
          "unlockReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.unlockReview sessionID)) ]

let assembleRegistrationObject
    (scope: RuntimeScope)
    (tools: ToolDefinition array)
    (wrappers: obj)
    (mcpServers: obj)
    (contextInjector: obj)
    (eventHook: obj)
    (slashCommands: obj)
    (messagesTransform: obj)
    (compactingTransform: obj)
    (getToolPolicy: obj)
    (reviewTestSurface: obj)
    : obj =
    createObj [
        "__runtimeScope", box scope
        "toolNames", box muxToolNames
        "tools", box tools
        "wrappers", box wrappers
        "mcpServers", box mcpServers
        "contextInjector", box contextInjector
        "eventHook", box eventHook
        "slashCommands", box slashCommands
        "messagesTransform", box messagesTransform
        "compactingTransform", box compactingTransform
        "getToolPolicy", box getToolPolicy
        "__reviewStore", box reviewTestSurface
        "tool.execute.after", box (System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            Wanxiangshu.Mux.PluginCatalog.toolExecuteAfter input output)) ]