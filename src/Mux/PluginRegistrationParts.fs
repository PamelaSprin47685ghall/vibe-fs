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
open Wanxiangshu.Mux.KnowledgeGraphRuntimeMux
open Wanxiangshu.Mux.KnowledgeGraphTestHooks

let createWrapperExecution (toolsObj: obj) (hostReadExec: HostFunctionCapture) (scope: RuntimeScope) : obj =
    createAllWrappers toolsObj hostReadExec scope

let createMessageTransforms
    (deps: obj)
    (scope: RuntimeScope)
    (backlogSession: BacklogSession)
    (knowledgeGraphRuntime: MuxKnowledgeGraphRuntime)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    : obj * obj =
    let messagesTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            messagesTransform deps scope backlogSession knowledgeGraphRuntime reviewStore input output)
    let compactingTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            compactingTransform deps backlogSession input output)
    (box messagesTransformFn, box compactingTransformFn)

let createEventHooksSlashAndPolicy
    (deps: obj)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (knowledgeGraphRuntime: MuxKnowledgeGraphRuntime)
    : obj * obj * obj =
    let eventHook = createEventHook deps reviewStore knowledgeGraphRuntime
    let slashCommands = createSlashCommands deps muxToolNames reviewStore
    let getToolPolicy = System.Func<string, obj, obj>(fun (_agentId: string) (role: obj) -> buildToolPolicy muxToolNames role)
    (box eventHook, box slashCommands, box getToolPolicy)

let createContextInjector () : obj =
    box {| inject = (fun (projectPath: string) ->
        promise {
            let! files = findCapsFiles projectPath
            return if List.isEmpty files then box null else box (Wanxiangshu.Kernel.CapsFormat.buildCapitalsContext files)
        } :> obj) |}

let createKnowledgeGraphTestSurface (knowledgeGraphRuntime: MuxKnowledgeGraphRuntime) : obj =
    let hooks = knowledgeGraphRuntime.TestHooks
    createObj
        [ "rawInstance", box knowledgeGraphRuntime
          "registerJobForTesting",
          box (System.Func<string, string, string, obj, unit>(fun sessionID workspaceRoot kindTag payload ->
              hooks.RegisterJob(sessionID, workspaceRoot, kindTag, payload)))
          "startMaintenanceIfDue",
          box (System.Func<string, JS.Promise<unit>>(fun workspaceRoot -> knowledgeGraphRuntime.StartMaintenanceIfDue(workspaceRoot)))
          "takeBookkeeperLaunchesForTesting", box (System.Func<obj array>(fun () -> hooks.TakeLaunches()))
          "waitForBackgroundJobsForTesting", box (System.Func<JS.Promise<unit>>(fun () -> hooks.WaitJobs()))
          "hasJobForTesting", box (System.Func<string, bool>(fun sessionID -> hooks.HasJob(sessionID))) ]

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
    (kgTestSurface: obj)
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
        "__knowledgeGraphRuntime", box kgTestSurface
        "__reviewStore", box reviewTestSurface ]