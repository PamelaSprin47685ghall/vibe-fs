module Wanxiangshu.Hosts.Opencode.HookTransform

open Fable.Core
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Opencode.ChatHooks
open Wanxiangshu.Hosts.Opencode.MessageTransformHook
open Wanxiangshu.Hosts.Opencode.SystemTransform
open Wanxiangshu.Hosts.Opencode.CompactionTransform
open Wanxiangshu.Hosts.Opencode.ToolDefinitionHooks
open Wanxiangshu.Hosts.Opencode.EventHooks
open Wanxiangshu.Runtime.ChildAgentRegistry

let chatMessageFor
    (host: Host)
    (registry: ChildAgentRegistry)
    (lifecycleObserver: Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver.SessionLifecycleObserver)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    ChatHooks.chatMessageFor host registry lifecycleObserver input output

let chatMessage
    (registry: ChildAgentRegistry)
    (lifecycleObserver: Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver.SessionLifecycleObserver)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    ChatHooks.chatMessage registry lifecycleObserver input output

let messagesTransform
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: Wanxiangshu.Hosts.Opencode.BacklogSession.BacklogSession)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (client: obj)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    MessageTransformHook.messagesTransform
        registry
        directory
        runtimeScope
        backlogSession
        reviewStore
        client
        input
        output

let systemTransform (directory: string) (input: obj) (output: obj) : JS.Promise<unit> =
    SystemTransform.systemTransform directory input output

let compactionAutocontinue (input: obj) (output: obj) : JS.Promise<unit> =
    CompactionTransform.compactionAutocontinue input output

let toolDefinitionFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    ToolDefinitionHooks.toolDefinitionFor host input output

let toolDefinition (input: obj) (output: obj) : JS.Promise<unit> =
    ToolDefinitionHooks.toolDefinition input output

let eventHandler
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (ctx: obj)
    (input: obj)
    : JS.Promise<unit> =
    EventHooks.eventHandler reviewStore scope ctx input

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = ChatHooks.noop _a _b
let noopEvent (_a: obj) : JS.Promise<unit> = ChatHooks.noopEvent _a
