module Wanxiangshu.Opencode.HookTransform

open Fable.Core
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Opencode.ChatHooks
open Wanxiangshu.Opencode.MessageTransform
open Wanxiangshu.Opencode.ToolDefinitionHooks
open Wanxiangshu.Opencode.EventHooks
open Wanxiangshu.Shell.ChildAgentRegistry

let chatMessageFor
    (host: Host)
    (registry: ChildAgentRegistry)
    (lifecycleObserver: Wanxiangshu.Opencode.SessionLifecycleObserver.SessionLifecycleObserver)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    ChatHooks.chatMessageFor host registry lifecycleObserver input output

let chatMessage
    (registry: ChildAgentRegistry)
    (lifecycleObserver: Wanxiangshu.Opencode.SessionLifecycleObserver.SessionLifecycleObserver)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    ChatHooks.chatMessage registry lifecycleObserver input output

let messagesTransform
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope)
    (backlogSession: Wanxiangshu.Opencode.BacklogSession.BacklogSession)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (client: obj)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    MessageTransform.messagesTransform registry directory runtimeScope backlogSession reviewStore client input output

let systemTransform (directory: string) (input: obj) (output: obj) : JS.Promise<unit> =
    MessageTransform.systemTransform directory input output

let toolDefinitionFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    ToolDefinitionHooks.toolDefinitionFor host input output

let toolDefinition (input: obj) (output: obj) : JS.Promise<unit> =
    ToolDefinitionHooks.toolDefinition input output

let eventHandler
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (scope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope)
    (input: obj)
    : JS.Promise<unit> =
    EventHooks.eventHandler reviewStore scope input

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = ChatHooks.noop _a _b
let noopEvent (_a: obj) : JS.Promise<unit> = ChatHooks.noopEvent _a
