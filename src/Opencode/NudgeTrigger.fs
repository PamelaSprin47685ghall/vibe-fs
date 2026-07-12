module Wanxiangshu.Opencode.NudgeTrigger

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Opencode.NudgeEffect
open Wanxiangshu.Opencode.FallbackHooksHelper

type NudgeTrigger
    (
        host: Host,
        ctx: obj,
        fallbackRuntime: Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState,
        reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore,
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
            resolveStatusValue (Dyn.get props "status") = "idle"
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
                    | "session.next.prompted" -> ()
                    | "session.status" ->
                        let statusObj = Dyn.get envelope.Props "status"

                        if not (Dyn.isNullish statusObj) then
                            let status = resolveStatusValue statusObj

                            if status = "interrupted" || status = "abort" then
                                markForceStopped sessionIDStr
                            elif status = "busy" then
                                ()
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
                    let isTest =
                        try
                            let p: obj = Fable.Core.JsInterop.emitJsExpr () "process"
                            p?env?("WANXIANGSHU_TEST") = "true"
                        with _ ->
                            false

                    let! owner =
                        promise {
                            let current = fallbackRuntime.GetSessionOwner sessionIDStr

                            if current <> "None" then
                                return current
                            elif not isTest then
                                return "None"
                            else
                                match getClientFromPluginCtx ctx with
                                | Error _ -> return "None"
                                | Ok client ->
                                    let arg = box {| path = box {| id = sessionIDStr |} |}
                                    let! resp = invokeClient client "messages" arg
                                    let data = Dyn.get resp "data"

                                    if not (Dyn.isNullish data) && Dyn.isArray data then
                                        let messagesArr = data :?> obj array

                                        if messagesArr.Length > 0 then
                                            let lastMsg = messagesArr.[messagesArr.Length - 1]
                                            let info = Dyn.get lastMsg "info"
                                            let role = Dyn.str info "role"
                                            if role = "assistant" then return "Human" else return "None"
                                        else
                                            return "None"
                                    else
                                        return "None"
                        }

                    let isForce = isForceStopped sessionIDStr

                    let origin =
                        if owner = "Human" then
                            if isForce then
                                TerminalOrigin.HumanTurnAborted
                            else
                                TerminalOrigin.HumanTurnCompleted
                        elif owner = "None" then
                            TerminalOrigin.Unknown
                        elif owner = "Fallback" then
                            TerminalOrigin.FallbackContinuationCompleted
                        elif owner = "Compaction" then
                            if
                                fallbackRuntime.IsCompacted sessionIDStr
                                && (isTest || fallbackRuntime.IsCompactionContinuationObserved sessionIDStr)
                            then
                                TerminalOrigin.CompactionContinuationCompleted
                            else
                                TerminalOrigin.Unknown
                        elif owner = "Nudge" then
                            TerminalOrigin.NudgeCompleted
                        elif owner = "Title" then
                            TerminalOrigin.TitleCompleted
                        else
                            TerminalOrigin.Unknown

                    if owner = "Fallback" || owner = "Nudge" || owner = "Title" then
                        fallbackRuntime.SetSessionOwner sessionIDStr "None"
                    elif
                        owner = "Compaction"
                        && fallbackRuntime.IsCompacted sessionIDStr
                        && (isTest || fallbackRuntime.IsCompactionContinuationObserved sessionIDStr)
                    then
                        let activeComp = fallbackRuntime.GetActiveCompactionId sessionIDStr

                        if activeComp <> "" then
                            let directory = pluginDirectoryFromCtx ctx

                            if directory <> "" then
                                do! appendCompactionSettledOrFail directory sessionIDStr activeComp "completed"

                        fallbackRuntime.SetSessionOwner sessionIDStr "None"
                        fallbackRuntime.SetCompacted sessionIDStr false
                        fallbackRuntime.SetCompactionContinuationObserved sessionIDStr false

                    let isEligible =
                        match origin with
                        | TerminalOrigin.HumanTurnCompleted when eventType <> "session.error" -> true
                        | _ -> false

                    if
                        NudgeTrigger.isNaturalStop eventType props
                        && isEligible
                        && not (reviewStore.getPendingReviewIds () |> List.contains sessionIDStr)
                    then
                        match getClientFromPluginCtx ctx with
                        | Ok client ->
                            try
                                fallbackRuntime.SetNudgeActive sessionIDStr true
                                fallbackRuntime.SetSessionOwner sessionIDStr "Nudge"

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
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (markForceStopped: string -> unit)
    (removeForceStopped: string -> unit)
    (isForceStopped: string -> bool)
    : NudgeTrigger =
    NudgeTrigger(host, ctx, fallbackRuntime, reviewStore, markForceStopped, removeForceStopped, isForceStopped)
