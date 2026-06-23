module VibeFs.Opencode.HookTransform

open Fable.Core
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.ChatHooks
open VibeFs.Opencode.MessageTransform
open VibeFs.Opencode.ToolDefinitionHooks
open VibeFs.Opencode.EventHooks
open VibeFs.Shell.ChildAgentRegistry

let chatMessageFor (host: Host) (registry: ChildAgentRegistry) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    ChatHooks.chatMessageFor host registry nudgeHook input output

let chatMessage (registry: ChildAgentRegistry) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    ChatHooks.chatMessage registry nudgeHook input output

let messagesTransform (registry: ChildAgentRegistry) (directory: string) (magicSession: VibeFs.Opencode.MagicTodo.MagicSession) (knowledgeGraphRuntime: VibeFs.Opencode.KnowledgeGraphRuntime.KnowledgeGraphRuntime) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (input: obj) (output: obj) : JS.Promise<unit> =
    MessageTransform.messagesTransform registry directory magicSession knowledgeGraphRuntime reviewStore input output

let compactingHandlerFor (host: Host) (magicSession: VibeFs.Opencode.MagicTodo.MagicSession) (input: obj) (output: obj) : JS.Promise<unit> =
    MessageTransform.compactingHandlerFor host magicSession input output

let compactingHandler (magicSession: VibeFs.Opencode.MagicTodo.MagicSession) (input: obj) (output: obj) : JS.Promise<unit> =
    MessageTransform.compactingHandler magicSession input output

let toolDefinitionFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    ToolDefinitionHooks.toolDefinitionFor host input output

let toolDefinition (input: obj) (output: obj) : JS.Promise<unit> =
    ToolDefinitionHooks.toolDefinition input output

let eventHandler (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (input: obj) : JS.Promise<unit> =
    EventHooks.eventHandler reviewStore input

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = ChatHooks.noop _a _b
let noopEvent (_a: obj) : JS.Promise<unit> = ChatHooks.noopEvent _a
