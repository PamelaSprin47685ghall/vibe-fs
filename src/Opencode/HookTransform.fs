module VibeFs.Opencode.HookTransform

open Fable.Core
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.ChatHooks
open VibeFs.Opencode.MessageTransform
open VibeFs.Opencode.ToolDefinitionHooks
open VibeFs.Opencode.EventHooks
open VibeFs.Shell.ChildAgentRegistry

let chatMessageFor (host: Host) (registry: ChildAgentRegistry) (lifecycleObserver: VibeFs.Opencode.SessionLifecycleObserver.SessionLifecycleObserver) (input: obj) (output: obj) : JS.Promise<unit> =
    ChatHooks.chatMessageFor host registry lifecycleObserver input output

let chatMessage (registry: ChildAgentRegistry) (lifecycleObserver: VibeFs.Opencode.SessionLifecycleObserver.SessionLifecycleObserver) (input: obj) (output: obj) : JS.Promise<unit> =
    ChatHooks.chatMessage registry lifecycleObserver input output

let messagesTransform (registry: ChildAgentRegistry) (directory: string) (runtimeScope: VibeFs.Shell.RuntimeScope.RuntimeScope) (backlogSession: VibeFs.Opencode.BacklogSession.BacklogSession) (knowledgeGraphRuntime: VibeFs.Opencode.KnowledgeGraphRuntime.KnowledgeGraphRuntime) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (input: obj) (output: obj) : JS.Promise<unit> =
    MessageTransform.messagesTransform registry directory runtimeScope backlogSession knowledgeGraphRuntime reviewStore input output

let compactingHandlerFor (host: Host) (backlogSession: VibeFs.Opencode.BacklogSession.BacklogSession) (input: obj) (output: obj) : JS.Promise<unit> =
    MessageTransform.compactingHandlerFor host backlogSession input output

let compactingHandler (backlogSession: VibeFs.Opencode.BacklogSession.BacklogSession) (input: obj) (output: obj) : JS.Promise<unit> =
    MessageTransform.compactingHandler backlogSession input output

let systemTransform (input: obj) (output: obj) : JS.Promise<unit> =
    MessageTransform.systemTransform input output

let toolDefinitionFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    ToolDefinitionHooks.toolDefinitionFor host input output

let toolDefinition (input: obj) (output: obj) : JS.Promise<unit> =
    ToolDefinitionHooks.toolDefinition input output

let eventHandler (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (input: obj) : JS.Promise<unit> =
    EventHooks.eventHandler reviewStore input

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = ChatHooks.noop _a _b
let noopEvent (_a: obj) : JS.Promise<unit> = ChatHooks.noopEvent _a
