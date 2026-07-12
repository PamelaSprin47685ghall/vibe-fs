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

let tryGetModelStringFromHook (input: obj) (output: obj) : string option =
    let candidates =
        [ Dyn.get input "model"
          (let msg = Dyn.get input "message" in

           if not (Dyn.isNullish msg) then
               Dyn.get msg "model"
           else
               null)
          (let info = Dyn.get input "info" in

           if not (Dyn.isNullish info) then
               Dyn.get info "model"
           else
               null)
          (let msg = chatMessageFromHookOutput output in if msg.IsSome then Dyn.get msg.Value "model" else null) ]

    candidates
    |> List.tryPick (fun mVal ->
        if Dyn.isNullish mVal then
            None
        elif Dyn.typeIs mVal "string" then
            let s = mVal :?> string
            if s <> "" then Some s else None
        else
            let providerID = Dyn.str mVal "providerID"
            let modelID = Dyn.str mVal "modelID"
            let variant = Dyn.str mVal "variant"
            let suffix = if variant <> "" then ":" + variant else ""

            if providerID = "" || modelID = "" then
                let idVal = Dyn.str mVal "id"
                if idVal <> "" then Some(idVal + suffix) else None
            else
                Some(sprintf "%s/%s%s" providerID modelID suffix))

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

        let msgObj = Dyn.get output "message"
        let msgId = if Dyn.isNullish msgObj then "" else Dyn.str msgObj "id"
        let sessionIDStr = Wanxiangshu.Kernel.Domain.Id.sessionIdValue sessionID
        let fr = lifecycleObserver.FallbackRuntime

        let isSystem =
            let owner = fr.GetSessionOwner sessionIDStr

            let hasPending =
                match fr.TryGetPendingLease sessionIDStr with
                | Some lease -> lease.Status = "dispatch_started"
                | None -> false

            if hasPending then
                if msgId <> "" then
                    fr.AddSystemMessageId sessionIDStr msgId "Fallback"

                true
            elif fr.IsNudgeActive sessionIDStr then
                if msgId <> "" then
                    fr.AddSystemMessageId sessionIDStr msgId "Nudge"

                true
            elif owner = "Compaction" then
                if msgId <> "" then
                    fr.AddSystemMessageId sessionIDStr msgId "Compaction"

                true
            else
                false

        if not isSystem then
            let modelOpt = tryGetModelStringFromHook input output
            do! lifecycleObserver.OnNewHumanMessage(sessionIDStr, agent, modelOpt)

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
