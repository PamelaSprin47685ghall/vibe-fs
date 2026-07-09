module Wanxiangshu.Opencode.SessionLifecycleObserver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Opencode.ProgressObserver
open Wanxiangshu.Opencode.FallbackCoordinator
open Wanxiangshu.Opencode.NudgeTrigger
open Wanxiangshu.Opencode.BacklogSession

type SessionLifecycleObserver
    (
        host: Host,
        ctx: obj,
        reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore,
        registry: ChildAgentRegistry,
        fallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option,
        fallbackRuntime: FallbackRuntimeState,
        backlogSession: Wanxiangshu.Opencode.BacklogSession.BacklogSession
    ) =

    let mutable forceStoppedSessions: Set<string> = Set.empty

    let resolvedUnitPromise () : JS.Promise<unit> = Promise.lift ()

    let progress = ProgressObserver(host, ctx, backlogSession, fallbackRuntime)
    let fallback = FallbackCoordinator(fallbackHandler, fallbackRuntime)

    let nudge =
        createNudgeTrigger
            host
            ctx
            fallbackRuntime
            (fun sid -> forceStoppedSessions <- Set.add sid forceStoppedSessions)
            (fun sid -> forceStoppedSessions <- Set.remove sid forceStoppedSessions)
            (fun sid -> Set.contains sid forceStoppedSessions)

    member _.handleChatMessage(sessionID: SessionId, agent: string, parts: obj) : JS.Promise<unit> =
        progress.OnChatMessage(sessionID, agent, parts)

    member _.handleCommandExecuteBefore (input: obj) (_output: obj) : JS.Promise<unit> =
        let _sessionIDStr = sessionIdFromHookInput input ""
        resolvedUnitPromise ()

    member _.handleToolExecuteAfter (input: obj) (output: obj) : JS.Promise<unit> =
        progress.HandleToolExecuteAfter input output

    member _.handleEvent(input: obj) : JS.Promise<unit> =
        promise {
            let eventEnvelope = decodeHostEventEnvelope input

            match eventEnvelope with
            | Some { EventType = "session.status"
                     Props = props } ->
                let statusObj = Dyn.get props "status"
                let agentName = Dyn.str statusObj "agent"
                let sid = getSessionID "session.status" props

                if sid <> "" then
                    if agentName <> "" then
                        fallbackRuntime.SetAgentName sid agentName

                    let modelObj = Dyn.get statusObj "model"

                    match Wanxiangshu.Shell.FallbackMessageCodec.decodeModelFromObj modelObj with
                    | Some m -> fallbackRuntime.SetModel sid m
                    | None -> ()
            | _ -> ()

            fallback.UpdateBusyCount eventEnvelope
            do! nudge.TrackLifetimeEvents eventEnvelope

            let! fbConsumed = fallback.TryConsumeEvent input

            if fbConsumed then
                return ()
            else
                do! nudge.HandleNaturalStop eventEnvelope
        }

let createSessionLifecycleObserver
    (
        host: Host,
        ctx: obj,
        reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore,
        registry: ChildAgentRegistry,
        fallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option,
        fallbackRuntime: FallbackRuntimeState,
        backlogSession: Wanxiangshu.Opencode.BacklogSession.BacklogSession
    ) : SessionLifecycleObserver =
    SessionLifecycleObserver(host, ctx, reviewStore, registry, fallbackHandler, fallbackRuntime, backlogSession)
