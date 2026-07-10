module Wanxiangshu.Opencode.NudgeTrigger

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Opencode.NudgeEffect

type NudgeTrigger
    (
        host: Host,
        ctx: obj,
        fallbackRuntime: Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState,
        markForceStopped: string -> unit,
        removeForceStopped: string -> unit,
        isForceStopped: string -> bool
    ) =

    /// Exposed as internal for unit testing of event-classification logic.
    static member internal isNaturalStop (eventType: string) (props: obj) : bool =
        if eventType = "session.idle" then
            true
        elif eventType = "session.error" then
            true
        elif eventType = "session.status" then
            let statusObj = Dyn.get props "status"

            let status =
                let fromStatus = Dyn.str statusObj "status"

                if fromStatus <> "" then
                    fromStatus
                else
                    Dyn.str statusObj "type"

            status = "idle"
        else
            false

    member _.TrackLifetimeEvents(eventEnvelope: HostEventEnvelope option) : JS.Promise<unit> =
        promise {
            match eventEnvelope with
            | Some envelope ->
                let sessionIDStr = getSessionID envelope.EventType envelope.Props

                if sessionIDStr <> "" then
                    match envelope.EventType with
                    | "stream-abort"
                    | "session.abort"
                    | "session.interrupted" -> markForceStopped sessionIDStr
                    | "session.error" ->
                        let errorObj = Dyn.get envelope.Props "error"

                        if not (Dyn.isNullish errorObj) then
                            let name = Dyn.str errorObj "name"
                            let tag = Dyn.str errorObj "_tag"
                            let msg = Dyn.str errorObj "message"

                            if
                                name = "AbortError"
                                || name = "MessageAbortedError"
                                || tag = "MessageAborted"
                                || containsAbortText msg
                            then
                                markForceStopped sessionIDStr
                    | "session.next.prompted" -> removeForceStopped sessionIDStr
                    | "session.status" ->
                        let statusObj = Dyn.get envelope.Props "status"

                        if not (Dyn.isNullish statusObj) then
                            let status =
                                let fromStatus = Dyn.str statusObj "status"

                                if fromStatus <> "" then
                                    fromStatus
                                else
                                    Dyn.str statusObj "type"

                            if status = "interrupted" || status = "abort" then
                                markForceStopped sessionIDStr
                    | "session.deleted"
                    | "session.delete"
                    | "session.remove"
                    | "session.close" ->
                        let directory = pluginDirectoryFromCtx ctx
                        do! appendNudgeDedupClearedOrFail directory sessionIDStr
                    | _ -> ()
            | None -> ()
        }

    member _.HandleNaturalStop(eventEnvelope: HostEventEnvelope option) : JS.Promise<unit> =
        promise {
            match eventEnvelope with
            | None -> ()
            | Some envelope ->
                let eventType = envelope.EventType
                let props = envelope.Props
                let sessionIDStr = getSessionID eventType props

                match Id.trySessionId sessionIDStr with
                | None -> ()
                | Some sessionID ->
                    if NudgeTrigger.isNaturalStop eventType props && not (isForceStopped sessionIDStr) then
                        match getClientFromPluginCtx ctx with
                        | Ok client ->
                            try
                                fallbackRuntime.SetNudgeActive sessionIDStr true

                                let dispatchPostStop
                                    : Host
                                          -> FallbackRuntimeState.FallbackRuntimeState
                                          -> obj
                                          -> obj
                                          -> SessionId
                                          -> (string -> bool)
                                          -> JS.Promise<unit> =
                                    dispatchPostStopFromHistory

                                do! dispatchPostStop host fallbackRuntime client ctx sessionID isForceStopped
                            finally
                                fallbackRuntime.SetNudgeActive sessionIDStr false
                        | Error _ -> ()
        }

let createNudgeTrigger
    (host: Host)
    (ctx: obj)
    (fallbackRuntime: Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState)
    (markForceStopped: string -> unit)
    (removeForceStopped: string -> unit)
    (isForceStopped: string -> bool)
    : NudgeTrigger =
    NudgeTrigger(host, ctx, fallbackRuntime, markForceStopped, removeForceStopped, isForceStopped)
