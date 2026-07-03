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
    ( host              : Host
    , ctx               : obj
    , markForceStopped  : string -> unit
    , removeForceStopped: string -> unit
    , isForceStopped    : string -> bool
    ) =

    let isNaturalStop (eventType: string) (props: obj) : bool =
        if eventType = "session.idle" then true
        elif eventType = "session.status" then
            let statusObj = Dyn.get props "status"
            let status =
                let fromStatus = Dyn.str statusObj "status"
                if fromStatus <> "" then fromStatus else Dyn.str statusObj "type"
            status = "idle"
        else false

    member _.TrackLifetimeEvents(eventEnvelope: HostEventEnvelope option) : JS.Promise<unit> =
        promise {
            match eventEnvelope with
            | Some envelope ->
                let sessionIDStr = getSessionID envelope.EventType envelope.Props
                if sessionIDStr <> "" then
                    match envelope.EventType with
                    | "stream-abort" ->
                        markForceStopped sessionIDStr
                    | "session.error" ->
                        let errorObj = Dyn.get envelope.Props "error"
                        if not (Dyn.isNullish errorObj) then
                            let name = Dyn.str errorObj "name"
                            let tag = Dyn.str errorObj "_tag"
                            let msg = Dyn.str errorObj "message"
                            if name = "AbortError" || name = "MessageAbortedError"
                               || tag = "MessageAborted"
                               || containsAbortText msg then
                                markForceStopped sessionIDStr
                    | "session.next.prompted" ->
                        removeForceStopped sessionIDStr
                    | "session.deleted" | "session.delete" | "session.remove" | "session.close" ->
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
                    if isNaturalStop eventType props
                       && not (isForceStopped sessionIDStr) then
                        match getClientFromPluginCtx ctx with
                        | Ok client ->
                            do! dispatchPostStopFromHistory host client ctx sessionID
                        | Error _ -> ()
        }

let createNudgeTrigger
    (host               : Host)
    (ctx                : obj)
    (markForceStopped   : string -> unit)
    (removeForceStopped : string -> unit)
    (isForceStopped     : string -> bool)
    : NudgeTrigger =
    NudgeTrigger(host, ctx, markForceStopped, removeForceStopped, isForceStopped)
