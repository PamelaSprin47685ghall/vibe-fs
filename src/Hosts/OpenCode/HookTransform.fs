module Wanxiangshu.Hosts.Opencode.HookTransform

open Fable.Core
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Opencode.ChatHooks
open Wanxiangshu.Hosts.Opencode.MessageTransformHook
open Wanxiangshu.Hosts.Opencode.EventHooks
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.ChatTransformOutputCodec

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
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (client: obj)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    MessageTransformHook.messagesTransform registry directory runtimeScope reviewStore client input output

let systemTransform (directory: string) (_input: obj) (output: obj) : JS.Promise<unit> =
    promise { setSystemOutputToDirectory directory output }

let compactionAutocontinue (_input: obj) (_output: obj) : JS.Promise<unit> = Promise.lift ()

let eventHandler
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (ctx: obj)
    (input: obj)
    : JS.Promise<unit> =
    EventHooks.eventHandler reviewStore scope ctx input

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = ChatHooks.noop _a _b
let noopEvent (_a: obj) : JS.Promise<unit> = ChatHooks.noopEvent _a
