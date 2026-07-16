module Wanxiangshu.Hosts.Opencode.NudgeTrigger

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.NudgeRuntimeTypes
open Wanxiangshu.Hosts.Opencode.NudgeEffect
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection

type NudgeTrigger
    (
        host: Host,
        ctx: obj,
        fallbackRuntime: FallbackRuntimeStore,
        reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore,
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

                            if current <> SessionOwner.NoOwner then
                                return current
                            elif not isTest then
                                return SessionOwner.NoOwner
                            else
                                match getClientFromPluginCtx ctx with
                                | Error _ -> return SessionOwner.NoOwner
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

                                            if role = "assistant" then
                                                return SessionOwner.Human
                                            else
                                                return SessionOwner.NoOwner
                                        else
                                            return SessionOwner.NoOwner
                                    else
                                        return SessionOwner.NoOwner
                        }

                    let isForce = isForceStopped sessionIDStr

                    let origin =
                        if owner = SessionOwner.Human then
                            if isForce then
                                TerminalOrigin.HumanTurnAborted
                            else
                                TerminalOrigin.HumanTurnCompleted
                        elif owner = SessionOwner.NoOwner then
                            TerminalOrigin.Unknown
                        elif owner = SessionOwner.Fallback then
                            TerminalOrigin.FallbackContinuationCompleted
                        elif owner = SessionOwner.Compaction then
                            if
                                fallbackRuntime.IsCompacted sessionIDStr
                                && (isTest || fallbackRuntime.IsCompactionContinuationObserved sessionIDStr)
                            then
                                TerminalOrigin.CompactionContinuationCompleted
                            else
                                TerminalOrigin.Unknown
                        elif owner = SessionOwner.Nudge then
                            TerminalOrigin.NudgeCompleted
                        elif owner = SessionOwner.Title then
                            TerminalOrigin.TitleCompleted
                        else
                            TerminalOrigin.Unknown

                    if owner = SessionOwner.Fallback || owner = SessionOwner.Title then
                        fallbackRuntime.SetSessionOwner sessionIDStr SessionOwner.NoOwner
                    elif owner = SessionOwner.Nudge then
                        match fallbackRuntime.TryGetPendingNudgeLease sessionIDStr with
                        | Some lease ->
                            let directory = pluginDirectoryFromCtx ctx

                            if directory <> "" then
                                do!
                                    finishNudge
                                        fallbackRuntime
                                        directory
                                        sessionIDStr
                                        lease
                                        NudgeOutcome.Settled
                                        "completed"
                                        ""
                                        ""
                        | None -> fallbackRuntime.SetSessionOwner sessionIDStr SessionOwner.NoOwner
                    elif
                        owner = SessionOwner.Compaction
                        && fallbackRuntime.IsCompacted sessionIDStr
                        && (isTest || fallbackRuntime.IsCompactionContinuationObserved sessionIDStr)
                    then
                        let activeComp = fallbackRuntime.GetActiveCompactionId sessionIDStr
                        let activeCompOrdinal = fallbackRuntime.GetActiveCompactionOrdinal sessionIDStr

                        if activeComp <> "" then
                            let directory = pluginDirectoryFromCtx ctx

                            if directory <> "" then
                                let settleInfo = fallbackRuntime.TryGetSettleInfo(sessionIDStr, activeComp)

                                match settleInfo with
                                | Some(_, ordinal) ->
                                    do!
                                        appendCompactionSettledOrFail
                                            directory
                                            sessionIDStr
                                            activeComp
                                            "completed"
                                            ordinal

                                    let _ = fallbackRuntime.ApplySettle(sessionIDStr, activeComp)
                                    ()
                                | None -> ()

                        fallbackRuntime.SetCompacted(sessionIDStr, false)
                        fallbackRuntime.SetCompactionContinuationObserved(sessionIDStr, false)

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

                                let dispatchPostStop
                                    : Host
                                          -> FallbackRuntimeStore
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
    (fallbackRuntime: FallbackRuntimeStore)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (markForceStopped: string -> unit)
    (removeForceStopped: string -> unit)
    (isForceStopped: string -> bool)
    : NudgeTrigger =
    NudgeTrigger(host, ctx, fallbackRuntime, reviewStore, markForceStopped, removeForceStopped, isForceStopped)
