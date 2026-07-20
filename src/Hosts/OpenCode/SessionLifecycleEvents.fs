module Wanxiangshu.Hosts.Opencode.SessionLifecycleEvents

open Fable.Core
open Wanxiangshu.Kernel.Session.SessionFact
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Hosts.Opencode.Fallback.Coordinator
open Wanxiangshu.Hosts.Opencode.NudgeTrigger
open Wanxiangshu.Hosts.Opencode.SessionLifecycleProcess
open Wanxiangshu.Runtime.Session.SessionActorRegistry
open Wanxiangshu.Runtime.Session.SessionActorState
open Wanxiangshu.Runtime.Session.SessionFactDecode

let private workspaceKeyFromCtx (ctx: obj) : string =
    let root = pluginDirectoryFromCtx ctx

    if root = "" then
        "opencode-default"
    else
        "opencode:" + root

/// Host event fan-out: decode → standard fact → session mailbox → return.
/// Domain mutation (owner/lease/nonce/generation/terminal) runs only inside the actor.
let handleEvent
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallback: FallbackCoordinator)
    (nudge: NudgeTrigger)
    (input: obj)
    : JS.Promise<unit> =
    promise {
        match SessionFactDecode.tryFromHostInput input with
        | Some(sid, fact) ->
            let actor = SessionActorRegistry.GetOrCreate (workspaceKeyFromCtx ctx) sid
            actor.BindHandler(fun snap f -> processLifecycleFact ctx fallbackRuntime fallback nudge sid snap f)
            let! _ = actor.Post fact
            ()
        | None ->
            match decodeHostEventEnvelope input with
            | None -> ()
            | Some env ->
                // Malformed / session-less envelope: still serialize via a scratch actor key.
                let sid = getSessionID env.EventType env.Props
                let fact = SessionFact.HostLifecycleEnvelope(env.EventType, env.Props, input)

                if sid = "" then
                    do!
                        processLifecycleFact
                            ctx
                            fallbackRuntime
                            fallback
                            nudge
                            ""
                            SessionActorSnapshot.empty
                            fact
                else
                    let actor = SessionActorRegistry.GetOrCreate (workspaceKeyFromCtx ctx) sid
                    actor.BindHandler(fun snap f -> processLifecycleFact ctx fallbackRuntime fallback nudge sid snap f)
                    let! _ = actor.Post fact
                    ()
    }
