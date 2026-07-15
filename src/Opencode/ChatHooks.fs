module Wanxiangshu.Opencode.ChatHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.ChatHookOutputCodec
open Wanxiangshu.Shell.OpencodeAgentConfigWire
open Wanxiangshu.Opencode.SubsessionHostAdapter

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

let tryGetNonceFromParts (parts: obj) : string option =
    if Dyn.isNullish parts || not (Dyn.isArray parts) then
        None
    else
        let arr = parts :?> obj array

        arr
        |> Array.tryPick (fun part ->
            let metadata = Dyn.get part "metadata"

            if Dyn.isNullish metadata then
                None
            else
                let nonce = Dyn.str metadata "nonce"
                if nonce <> "" then Some nonce else None)

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

        let parts = partsFromHookOutput output
        do! lifecycleObserver.handleChatMessage (sessionID, agent, parts)

        let msgObj = Dyn.get output "message"
        let msgId = if Dyn.isNullish msgObj then "" else Dyn.str msgObj "id"
        let sessionIDStr = Wanxiangshu.Kernel.Domain.Id.sessionIdValue sessionID
        let fr = lifecycleObserver.FallbackRuntime

        let isSystem =
            let nonceOpt = tryGetNonceFromParts parts

            match nonceOpt with
            | Some nonce ->
                // Child subsession turn marker: ChatHooks resolves the host
                // receipt for SubsessionActor. Never forges TurnStarted from
                // the prompt Promise alone.
                if
                    PendingTurnReceipt.tryResolve nonce (Wanxiangshu.Kernel.Subsession.Types.UserMessageObserved msgId)
                then
                    true
                else
                    let activeNudgeNonce = fr.GetActiveNudgeNonce sessionIDStr

                    if activeNudgeNonce <> "" && nonce = activeNudgeNonce then
                        true
                    else
                        match fr.TryGetPendingLease sessionIDStr with
                        | Some lease when
                            (lease.Status = LeaseStatus.DispatchStarted
                             || lease.Status = LeaseStatus.Dispatched
                             || lease.Status = LeaseStatus.Running)
                            && lease.ContinuationID = nonce
                            ->
                            true
                        | _ -> false
            | None -> false

        if not isSystem then
            let modelOpt = tryGetModelStringFromHook input output
            do! lifecycleObserver.OnNewHumanMessage(sessionIDStr, agent, modelOpt, msgId)

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
