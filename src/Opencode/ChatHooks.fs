module Wanxiangshu.Opencode.ChatHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.ChatHookOutputCodec
open Wanxiangshu.Shell.OpencodeAgentConfigWire

let private resolveAgent (registry: ChildAgentRegistry) (input: obj) (output: obj) : string =
    resolveHookAgent registry input (Some output) "manager"

let chatMessageFor
    (host: Host)
    (registry: ChildAgentRegistry)
    (lifecycleObserver: Wanxiangshu.Opencode.SessionLifecycleObserver.SessionLifecycleObserver)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        let agent = resolveAgent registry input output

        let sessionID =
            Wanxiangshu.Kernel.Domain.Id.sessionIdQuick (sessionIdFromHookInput input "")

        do! lifecycleObserver.handleChatMessage (sessionID, agent, partsFromHookOutput output)

        match chatMessageFromHookOutput output with
        | None -> ()
        | Some message ->
            match chatMessageToolsFromOutput output with
            | None -> ()
            | Some tools ->
                match filterChatToolsForAgent host agent (Some tools) with
                | None -> ()
                | Some filtered -> setKey message "tools" (encodeToolsOverridesToMessage filtered)
    }

let chatMessage
    (registry: ChildAgentRegistry)
    (lifecycleObserver: Wanxiangshu.Opencode.SessionLifecycleObserver.SessionLifecycleObserver)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    chatMessageFor opencode registry lifecycleObserver input output

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = Promise.lift ()
let noopEvent (_a: obj) : JS.Promise<unit> = Promise.lift ()
