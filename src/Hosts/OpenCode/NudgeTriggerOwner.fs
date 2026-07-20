module Wanxiangshu.Hosts.Opencode.NudgeTriggerOwner

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Runtime.NudgeEventWriter
open Wanxiangshu.Runtime.NudgeLease
open Wanxiangshu.Hosts.Opencode.NudgeEffect
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Hosts.Opencode.NudgeTriggerCleanup

let private tryRecoverOwnerFromHostHistory
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionIDStr: string)
    : JS.Promise<SessionOwner> =
    promise {
        match getClientFromPluginCtx ctx with
        | Error _ -> return SessionOwner.NoOwner
        | Ok client ->
            let arg = box {| path = box {| id = sessionIDStr |} |}
            let! resp = invokeClient client "messages" arg
            let data = Dyn.get resp "data"

            if Dyn.isNullish data || not (Dyn.isArray data) then
                return SessionOwner.NoOwner
            else
                let messagesArr = data :?> obj array

                let lastNativeUserIdx =
                    messagesArr
                    |> Array.tryFindIndexBack (fun msg ->
                        let info = Dyn.get msg "info"
                        let role = Dyn.str info "role"
                        let isSynthetic = Dyn.get info "synthetic" |> string = "true"
                        role = "user" && not isSynthetic)

                match lastNativeUserIdx with
                | None -> return SessionOwner.NoOwner
                | Some idx ->
                    let userMsgId = Dyn.str (Dyn.get messagesArr.[idx] "info") "id"

                    let hasCausality =
                        messagesArr.[idx + 1 ..]
                        |> Array.exists (fun msg ->
                            let info = Dyn.get msg "info"
                            let role = Dyn.str info "role"
                            role = "assistant" && Dyn.str info "parentID" = userMsgId)

                    let session = fallbackRuntime.GetSession sessionIDStr

                    let hasActiveLease =
                        (match session.PendingLease with
                         | Some l -> l.Status <> LeaseStatus.Settled && l.Status <> LeaseStatus.Cancelled
                         | None -> false)
                        || (match session.PendingNudgeLease with
                            | Some l -> l.Status <> LeaseStatus.Settled && l.Status <> LeaseStatus.Cancelled
                            | None -> false)
                        || session.CompactionActiveId <> ""

                    if hasCausality && not hasActiveLease then
                        return SessionOwner.Human
                    else
                        return SessionOwner.NoOwner
    }

let private performOwnerUnknownDiagnosticLogging
    (directory: string)
    (sessionIDStr: string)
    (reason: string)
    (isTest: bool)
    (props: obj)
    (hostEventType: string)
    (session: FallbackSessionRuntime)
    : JS.Promise<unit> =
    promise {
        let (episodeId, activeLeaseKind, activeLeaseId) =
            match session.ActiveEpisode with
            | Some ep -> (ep.EpisodeId, string ep.Kind, Option.defaultValue "" ep.LeaseId)
            | None -> ("", "", "")

        let hostEventId =
            let eid = Dyn.str props "id"

            if eid <> "" then
                eid
            else
                let evObj = Dyn.get props "eventId"
                if Dyn.isNullish evObj then "evt-unknown" else string evObj

        JS.console.warn (
            box
                {| feature = "nudge"
                   session = sessionIDStr
                   event = "nudge_owner_unknown"
                   reason = reason
                   isTest = isTest
                   directory = directory
                   hostEventId = hostEventId
                   hostEventType = hostEventType
                   episodeId = episodeId
                   generation = session.SessionGeneration
                   runtimeOwner = string session.Owner
                   actorOwner = string session.Owner
                   lastHumanTurnId = session.HumanTurnId
                   lastHumanMessageId = session.LastHumanMessageId
                   activeLeaseKind = activeLeaseKind
                   activeLeaseId = activeLeaseId
                   restoreStatus = string session.Core.Lifecycle
                   previousTerminalEventId = "" |}
        )

        try
            do! appendNudgeOwnerUnknownOrFail directory sessionIDStr reason
        with ex ->
            JS.console.error (
                box
                    {| feature = "nudge"
                       session = sessionIDStr
                       event = "nudge_owner_unknown"
                       error = "Failed to append nudge_owner_unknown: " + ex.Message |}
            )
    }

let private emitOwnerUnknownDiagnostic
    (directory: string)
    (sessionIDStr: string)
    (reason: string)
    (isTest: bool)
    (fallbackRuntime: FallbackRuntimeStore)
    (props: obj)
    (hostEventType: string)
    : JS.Promise<unit> =
    promise {
        let session = fallbackRuntime.GetSession sessionIDStr
        let isConsumed = session.TerminalConsumed

        if not isConsumed then
            fallbackRuntime.Update(sessionIDStr, setTerminalConsumed true)

            do! performOwnerUnknownDiagnosticLogging directory sessionIDStr reason isTest props hostEventType session
    }

let private inferOwnerFromTestRun (isTest: bool) (messages: obj) : SessionOwner =
    if not isTest then
        SessionOwner.NoOwner
    else if Dyn.isNullish messages || not (Dyn.isArray messages) then
        SessionOwner.NoOwner
    else
        let arr = messages :?> obj array

        if arr.Length = 0 then
            SessionOwner.NoOwner
        else
            let last = arr.[arr.Length - 1]
            let info = Dyn.get last "info"
            let role = Dyn.str info "role"

            if role = "assistant" || role = "toolResult" then
                SessionOwner.Human
            else
                SessionOwner.NoOwner

/// Read the current owner; in production and test, recover via causal history.
let resolveOwner
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionIDStr: string)
    (isTest: bool)
    (props: obj)
    (hostEventType: string)
    : JS.Promise<SessionOwner> =
    promise {
        let session = fallbackRuntime.GetSession sessionIDStr

        if isTerminalConsumed session then
            return session.Owner
        else
            let current = session.Owner

            if current <> SessionOwner.NoOwner then
                return current
            else
                let! inferred = tryRecoverOwnerFromHostHistory ctx fallbackRuntime sessionIDStr

                if inferred <> SessionOwner.NoOwner then
                    return inferred
                else
                    let mutable testOwner = SessionOwner.NoOwner

                    if isTest then
                        match getClientFromPluginCtx ctx with
                        | Ok client ->
                            let arg = box {| path = box {| id = sessionIDStr |} |}
                            let! resp = invokeClient client "messages" arg
                            let data = Dyn.get resp "data"
                            testOwner <- inferOwnerFromTestRun isTest data
                        | _ -> ()

                    if testOwner <> SessionOwner.NoOwner then
                        return testOwner
                    else
                        let directory = pluginDirectoryFromCtx ctx
                        let reason = "No owner inferred from runtime state or host messages"

                        do!
                            emitOwnerUnknownDiagnostic
                                directory
                                sessionIDStr
                                reason
                                isTest
                                fallbackRuntime
                                props
                                hostEventType

                        return SessionOwner.NoOwner
    }
