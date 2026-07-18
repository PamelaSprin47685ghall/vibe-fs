module Wanxiangshu.Hosts.Opencode.NudgeTrigger

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Hosts.Opencode.NudgeEffect
open Wanxiangshu.Hosts.Opencode.NudgeTriggerOps

/// Decide whether a natural-stop event should re-evaluate the nudge
/// state machine. Internal so regression tests can bind to the real
/// gate instead of re-encoding the rule. The dedup anchor, max-retry
/// count and `PendingNudge` state inside the nudge kernel remain the
/// authority on whether a nudge is actually emitted; this gate is
/// only the "should we look again?" question.
let internal isNudgeEvaluationEligible (origin: TerminalOrigin) (eventType: string) : bool =
    match origin with
    | TerminalOrigin.HumanTurnCompleted
    | TerminalOrigin.NudgeCompleted
    | TerminalOrigin.FallbackContinuationCompleted -> eventType <> "session.error"
    | _ -> false

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
                let sessionIDStr = getSessionID eventType envelope.Props

                match Id.trySessionId sessionIDStr with
                | None -> ()
                | Some sessionID ->
                    let isTest =
                        try
                            let p: obj = Fable.Core.JsInterop.emitJsExpr () "process"
                            p?env?("WANXIANGSHU_TEST") = "true"
                        with _ ->
                            false

                    let! owner = NudgeTriggerOps.resolveOwner ctx fallbackRuntime sessionIDStr isTest
                    let isForce = isForceStopped sessionIDStr

                    let origin =
                        NudgeTriggerOps.resolveOrigin fallbackRuntime owner isForce sessionIDStr

                    do! NudgeTriggerOps.applyPostTerminalCleanup ctx fallbackRuntime owner sessionIDStr
                    let isEligible = NudgeTriggerOps.isNudgeEligible origin eventType

                    if
                        NudgeTrigger.isNaturalStop eventType envelope.Props
                        && isEligible
                        && not (reviewStore.getPendingReviewIds () |> List.contains sessionIDStr)
                    then
                        match getClientFromPluginCtx ctx with
                        | Ok client ->
                            fallbackRuntime.Update(sessionIDStr, setNudgeActive true)

                            let! _ignored =
                                dispatchPostStopFromHistory host fallbackRuntime client ctx sessionID isForceStopped

                            fallbackRuntime.Update(sessionIDStr, setNudgeActive false)
                        | Error _ -> ()
        }

    member _.SettleCompactionIfCompleted(sessionIDStr: string) : JS.Promise<unit> =
        promise {
            if
                (fallbackRuntime.GetSession sessionIDStr).Owner = SessionOwner.Compaction
                && (fallbackRuntime.GetSession sessionIDStr).CompactionCompacted
                && (fallbackRuntime.GetSession sessionIDStr).CompactionContinuationObserved
            then
                do! NudgeTriggerOps.settleCompaction_ ctx fallbackRuntime sessionIDStr
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
