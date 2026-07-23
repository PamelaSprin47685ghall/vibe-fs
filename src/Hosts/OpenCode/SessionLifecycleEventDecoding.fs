module Wanxiangshu.Hosts.Opencode.SessionLifecycleEventDecoding

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Hosts.Opencode.NudgeTriggerOps
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Runtime.MessageTransform.CapsStage
open Wanxiangshu.Runtime.RuntimeScope

/// Apply mutations from a session.status event (agent name + model).
let handleSessionStatus (fallbackRuntime: FallbackRuntimeStore) (sid: string) (statusObj: obj) (agentName: string) =
    if agentName <> "" then
        fallbackRuntime.UpdateSession(sid, recordAgentName agentName)

    let modelObj = get statusObj "model"

    match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.decodeModelFromObj modelObj with
    | Some m -> fallbackRuntime.UpdateSession(sid, selectModel m)
    | None -> ()

/// Apply mutations from a session.compacted event. Async because it writes
/// to disk via appendCompactionContextGenerationChangedOrFail.
let handleSessionCompacted
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (runtimeScope: RuntimeScope)
    (sid: string)
    : JS.Promise<unit> =
    promise {
        if sid <> "" then
            invalidateCapsAfterCompaction runtimeScope sid

        let currentOwner = (fallbackRuntime.GetSession sid).Owner
        let session = fallbackRuntime.GetSession sid
        let compactionGen = session.CompactionGeneration
        let compactionId = session.CompactionActiveId
        let compactionOrdinal = session.CompactionActiveOrdinal

        if
            currentOwner = SessionOwner.Compaction
            && compactionId <> ""
            && not session.CompactionCompacted
        then
            let nextContextGen = compactionGen + 1
            fallbackRuntime.UpdateSession(sid, setCompactionGeneration nextContextGen)
            let directory = pluginDirectoryFromCtx ctx

            do!
                appendCompactionContextGenerationChangedOrFail
                    directory
                    sid
                    nextContextGen
                    compactionId
                    compactionOrdinal

            fallbackRuntime.Update(sid, setCompacted true)
            do! settleCompaction_ ctx fallbackRuntime sid
    }

/// Apply mutations from a message.updated event (compaction continuation detection).
let handleMessageUpdated (fallbackRuntime: FallbackRuntimeStore) (sid: string) (props: obj) =
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
                let currentOwner = (fallbackRuntime.GetSession sid).Owner

                if currentOwner = SessionOwner.Compaction then
                    fallbackRuntime.Update(sid, setCompactionContinuationObserved true)

let private handleBusyAfterTaskComplete (fallbackRuntime: FallbackRuntimeStore) (sid: string) =
    let state = fallbackRuntime.GetOrCreateState sid

    if state.Lifecycle = FallbackLifecycle.TaskComplete then
        fallbackRuntime.Update(
            sid,
            fun session ->
                { session with
                    Core =
                        { session.Core with
                            Lifecycle = FallbackLifecycle.Active } }
        )

let private handleSessionStatusEnvelope (fallbackRuntime: FallbackRuntimeStore) (props: obj) =
    let statusObj = get props "status"
    let agentName = str statusObj "agent"
    let sid2 = getSessionID "session.status" props

    if sid2 <> "" then
        handleSessionStatus fallbackRuntime sid2 statusObj agentName

        if resolveStatusValue statusObj = "busy" then
            handleBusyAfterTaskComplete fallbackRuntime sid2

/// Process the event-envelope match body: route idle + keep existing handlers.
let processEventEnvelope
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (runtimeScope: RuntimeScope)
    (sid: string)
    (eventEnvelope: HostEventEnvelope option)
    : JS.Promise<unit> =
    promise {
        match eventEnvelope with
        | Some { EventType = "session.status"
                 Props = props } -> handleSessionStatusEnvelope fallbackRuntime props

        | Some { EventType = "session.idle"
                 Props = _props } -> ()

        | Some { EventType = "session.compacted"
                 Props = props } ->
            let sid2 = getSessionID "session.compacted" props

            if sid2 <> "" then
                do! handleSessionCompacted ctx fallbackRuntime runtimeScope sid2

        | Some { EventType = "message.updated"
                 Props = props } ->
            let sid2 = getSessionID "message.updated" props

            if sid2 <> "" then
                handleMessageUpdated fallbackRuntime sid2 props

        | _ -> ()
    }

/// True when the envelope is a session.idle or a session.status whose value is "idle".
let isIdleEnvelope (env: HostEventEnvelope) =
    if env.EventType = "session.idle" then
        true
    elif env.EventType = "session.status" then
        let statusObj = get env.Props "status"
        not (isNullish statusObj) && resolveStatusValue statusObj = "idle"
    else
        false
