module Wanxiangshu.Hosts.Opencode.SessionLifecycleEvents

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.OpencodeHostEvent
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateFlagTransitions
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Hosts.Opencode.Fallback.Coordinator
open Wanxiangshu.Hosts.Opencode.NudgeTrigger

/// Apply mutations from a session.status event (agent name + model).
let private handleSessionStatus
    (fallbackRuntime: FallbackRuntimeStore)
    (sid: string)
    (statusObj: obj)
    (agentName: string)
    =
    if agentName <> "" then
        fallbackRuntime.SetAgentName sid agentName

    let modelObj = get statusObj "model"

    match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.decodeModelFromObj modelObj with
    | Some m -> fallbackRuntime.SetModel sid m
    | None -> ()

/// Apply mutations from a session.compacted event. Async because it writes
/// to disk via appendCompactionContextGenerationChangedOrFail.
let private handleSessionCompacted (ctx: obj) (fallbackRuntime: FallbackRuntimeStore) (sid: string) : JS.Promise<unit> =
    promise {
        let currentOwner = fallbackRuntime.GetSessionOwner sid
        let compactionGen = fallbackRuntime.GetCompactionGeneration sid
        let compactionId = fallbackRuntime.GetActiveCompactionId sid
        let compactionOrdinal = fallbackRuntime.GetActiveCompactionOrdinal sid

        if
            currentOwner = SessionOwner.Compaction
            && compactionId <> ""
            && not (fallbackRuntime.IsCompacted sid)
        then
            let nextContextGen = compactionGen + 1
            fallbackRuntime.SetCompactionGeneration(sid, nextContextGen)
            let directory = pluginDirectoryFromCtx ctx

            do!
                appendCompactionContextGenerationChangedOrFail
                    directory
                    sid
                    nextContextGen
                    compactionId
                    compactionOrdinal

            fallbackRuntime.SetCompacted(sid, true)
    }

/// Apply mutations from a message.updated event (compaction continuation detection).
let private handleMessageUpdated (fallbackRuntime: FallbackRuntimeStore) (sid: string) (props: obj) =
    let info = get props "info"
    let role = str info "role"

    if role = "user" then
        let parts = get props "parts"

        if not (isNullish parts) && isArray parts then
            let partsArr = parts :?> obj array

            let isCompactionContinue =
                partsArr
                |> Array.exists (fun part ->
                    let isSynth = get part "synthetic"
                    let meta = get part "metadata"

                    (not (isNullish isSynth) && unbox<bool> isSynth)
                    && (not (isNullish meta) && (get meta "compaction_continue" |> unbox<bool>)))

            if isCompactionContinue then
                let currentOwner = fallbackRuntime.GetSessionOwner sid

                if currentOwner = SessionOwner.Compaction && fallbackRuntime.IsCompacted sid then
                    fallbackRuntime.SetCompactionContinuationObserved(sid, true)

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
            fallbackRuntime.SetEventHandlingActive sid true

        try
            match eventEnvelope with
            | Some { EventType = "session.status"
                     Props = props } ->
                let statusObj = get props "status"
                let agentName = str statusObj "agent"
                let sid = getSessionID "session.status" props

                if sid <> "" then
                    handleSessionStatus fallbackRuntime sid statusObj agentName

            | Some { EventType = "session.compacted"
                     Props = props } ->
                let sid = getSessionID "session.compacted" props

                if sid <> "" then
                    do! handleSessionCompacted ctx fallbackRuntime sid

            | Some { EventType = "message.updated"
                     Props = props } ->
                let sid = getSessionID "message.updated" props

                if sid <> "" then
                    handleMessageUpdated fallbackRuntime sid props

            | _ -> ()

            fallback.UpdateBusyCount eventEnvelope
            do! nudge.TrackLifetimeEvents eventEnvelope

            let! fbConsumed = fallback.TryConsumeEvent input

            if fbConsumed then
                return ()
            else
                do! nudge.HandleNaturalStop eventEnvelope
        finally
            if sid <> "" then
                fallbackRuntime.SetEventHandlingActive sid false
    }
