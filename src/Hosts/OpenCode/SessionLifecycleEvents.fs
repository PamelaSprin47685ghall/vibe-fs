module Wanxiangshu.Hosts.Opencode.SessionLifecycleEvents

open Fable.Core
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Hosts.Opencode.Fallback.Coordinator
open Wanxiangshu.Hosts.Opencode.NudgeTrigger
open Wanxiangshu.Hosts.Opencode.NudgeTriggerOps
open Wanxiangshu.Hosts.Opencode.ChatHooksMessageIdDedup
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Hosts.Opencode.SessionLifecycleEventDecoding
open Wanxiangshu.Hosts.Opencode.SessionLifecycleHumanDispatch

let private handleSessionClosed (ctx: obj) (sid: string) (eventEnvelope: HostEventEnvelope option) : unit =
    if
        eventEnvelope
        |> Option.exists (fun env ->
            env.EventType = "session.deleted"
            || env.EventType = "session.delete"
            || env.EventType = "session.remove"
            || env.EventType = "session.close")
    then
        let ws =
            Wanxiangshu.Kernel.Primitives.Identity.Id.workspaceIdQuick ("opencode:" + (pluginDirectoryFromCtx ctx))

        sharedDispatchRegistry.NotifySessionClosed ws sid
        forget sid

/// Host event fan-out: session.status / compacted / message.updated + fallback/nudge.
let handleEvent
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallback: FallbackCoordinator)
    (nudge: NudgeTrigger)
    (input: obj)
    : JS.Promise<unit> =
    promise {
        let eventEnvelope = decodeHostEventEnvelope input

        let sid =
            match eventEnvelope with
            | Some env -> getSessionID env.EventType env.Props
            | None -> ""

        if sid <> "" then
            fallbackRuntime.Update(sid, setEventHandlingActive true)

        try
            do! processEventEnvelope ctx fallbackRuntime sid eventEnvelope
            settleChildDispatch ctx eventEnvelope

            fallback.UpdateBusyCount eventEnvelope
            do! nudge.TrackLifetimeEvents eventEnvelope

            let! fbConsumed = fallback.TryConsumeEvent input

            if fbConsumed then
                if Option.exists isIdleEnvelope eventEnvelope then
                    do! nudge.SettleCompactionIfCompleted sid
                    do! tryIdle (pluginDirectoryFromCtx ctx) sid |> Promise.map ignore
            else
                let activeFallbackOwnsTerminal =
                    sid <> ""
                    && Option.exists (fun env -> NudgeTrigger.isNaturalStop env.EventType env.Props) eventEnvelope
                    && hasActiveFallbackContinuation fallbackRuntime sid

                if not activeFallbackOwnsTerminal then
                    do! nudge.HandleNaturalStop eventEnvelope

                if Option.exists isIdleEnvelope eventEnvelope then
                    do! tryIdle (pluginDirectoryFromCtx ctx) sid |> Promise.map ignore
        finally
            if sid <> "" then
                fallbackRuntime.Update(sid, setEventHandlingActive false)
                handleSessionClosed ctx sid eventEnvelope
    }
