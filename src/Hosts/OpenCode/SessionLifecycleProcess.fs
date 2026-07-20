module Wanxiangshu.Hosts.Opencode.SessionLifecycleProcess

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Session.SessionFact
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Hosts.Opencode.Fallback.Coordinator
open Wanxiangshu.Hosts.Opencode.NudgeTrigger
open Wanxiangshu.Hosts.Opencode.NudgeTriggerOps
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Hosts.Opencode.SessionLifecycleEventDecoding
open Wanxiangshu.Hosts.Opencode.SessionLifecycleHumanDispatch
open Wanxiangshu.Hosts.Opencode.SessionLifecycleClose
open Wanxiangshu.Runtime.Session.SessionActorState
open Wanxiangshu.Runtime.SubsessionEventRouter

let private envelopeOf (eventType: string) (props: obj) : HostEventEnvelope =
    { EventType = eventType; Props = props }

let private rawOf (eventType: string) (props: obj) : obj =
    createObj [ "event" ==> createObj [ "type" ==> eventType; "properties" ==> props ] ]

/// Rebuild host envelope + raw input from a standardized fact for existing fan-out.
let private toHostSurface (fact: SessionFact) : (HostEventEnvelope * obj) option =
    match fact with
    | SessionFact.HostLifecycleEnvelope(eventType, props, rawInput) ->
        Some(envelopeOf eventType props, rawInput)
    | SessionFact.SessionBusyObserved props ->
        Some(envelopeOf "session.status" props, rawOf "session.status" props)
    | SessionFact.SessionIdleObserved props ->
        Some(envelopeOf "session.idle" props, rawOf "session.idle" props)
    | SessionFact.SessionErrorObserved props ->
        Some(envelopeOf "session.error" props, rawOf "session.error" props)
    | SessionFact.AssistantObserved(_, _, props)
    | SessionFact.ChatMessageObserved(_, _, props) ->
        Some(envelopeOf "message.updated" props, rawOf "message.updated" props)
    | SessionFact.SessionClosed ->
        let props = createObj [ "sessionID" ==> "" ]
        Some(envelopeOf "session.deleted" props, rawOf "session.deleted" props)
    | _ -> None

let private runHostFanOut
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallback: FallbackCoordinator)
    (nudge: NudgeTrigger)
    (sid: string)
    (envelope: HostEventEnvelope)
    (rawInput: obj)
    : JS.Promise<unit> =
    promise {
        if sid <> "" then
            fallbackRuntime.Update(sid, setEventHandlingActive true)

        try
            let envOpt = Some envelope
            do! processEventEnvelope ctx fallbackRuntime sid envOpt
            settleChildDispatch ctx envOpt
            fallback.UpdateBusyCount envOpt
            do! nudge.TrackLifetimeEvents envOpt
            let! fbConsumed = fallback.TryConsumeEvent rawInput

            if fbConsumed then
                if isIdleEnvelope envelope then
                    do! nudge.SettleCompactionIfCompleted sid
                    do! tryIdle (pluginDirectoryFromCtx ctx) sid |> Promise.map ignore
            else
                let activeFallbackOwnsTerminal =
                    sid <> ""
                    && NudgeTrigger.isNaturalStop envelope.EventType envelope.Props
                    && hasActiveFallbackContinuation fallbackRuntime sid

                if not activeFallbackOwnsTerminal then
                    do! nudge.HandleNaturalStop envOpt

                if isIdleEnvelope envelope then
                    do! tryIdle (pluginDirectoryFromCtx ctx) sid |> Promise.map ignore
        finally
            if sid <> "" then
                fallbackRuntime.Update(sid, setEventHandlingActive false)
    }

/// Domain work for one admitted lifecycle fact. Runs only inside SessionActor.
let processLifecycleFact
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallback: FallbackCoordinator)
    (nudge: NudgeTrigger)
    (sid: string)
    (_snap: SessionActorSnapshot)
    (fact: SessionFact)
    : JS.Promise<unit> =
    promise {
        match fact with
        | SessionFact.SessionClosed ->
            // Cleanup only — do not re-enter NotifyClosed/Post (deadlocks the actor queue).
            let closedEnv = envelopeOf "session.deleted" (createObj [ "sessionID" ==> sid ])
            nudge.TrackLifetimeEvents (Some closedEnv) |> Promise.start
            finalizeSessionClosed ctx sid
        | _ ->
            match toHostSurface fact with
            | Some(envelope, rawInput) -> do! runHostFanOut ctx fallbackRuntime fallback nudge sid envelope rawInput
            | None -> ()
    }
