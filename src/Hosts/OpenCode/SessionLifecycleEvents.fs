module Wanxiangshu.Hosts.Opencode.SessionLifecycleEvents

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Hosts.Opencode.Fallback.Coordinator
open Wanxiangshu.Hosts.Opencode.NudgeTrigger
open Wanxiangshu.Hosts.Opencode.NudgeTriggerOps
open Wanxiangshu.Hosts.Opencode.ChatHooksMessageIdDedup
open Wanxiangshu.Runtime.Dispatch

/// Apply mutations from a session.status event (agent name + model).
let private handleSessionStatus
    (fallbackRuntime: FallbackRuntimeStore)
    (sid: string)
    (statusObj: obj)
    (agentName: string)
    =
    if agentName <> "" then
        fallbackRuntime.UpdateSession(sid, recordAgentName agentName)

    let modelObj = get statusObj "model"

    match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.decodeModelFromObj modelObj with
    | Some m -> fallbackRuntime.UpdateSession(sid, selectModel m)
    | None -> ()

/// Apply mutations from a session.compacted event. Async because it writes
/// to disk via appendCompactionContextGenerationChangedOrFail.
let private handleSessionCompacted (ctx: obj) (fallbackRuntime: FallbackRuntimeStore) (sid: string) : JS.Promise<unit> =
    promise {
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
                let currentOwner = (fallbackRuntime.GetSession sid).Owner

                if currentOwner = SessionOwner.Compaction then
                    fallbackRuntime.Update(sid, setCompactionContinuationObserved true)

/// Process the event-envelope match body: route idle + keep existing handlers.
let private processEventEnvelope
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (sid: string)
    (eventEnvelope: HostEventEnvelope option)
    : JS.Promise<unit> =
    promise {
        match eventEnvelope with
        | Some { EventType = "session.status"
                 Props = props } ->
            let statusObj = get props "status"
            let agentName = str statusObj "agent"
            let sid2 = getSessionID "session.status" props

            if sid2 <> "" then
                handleSessionStatus fallbackRuntime sid2 statusObj agentName

                let statusVal = resolveStatusValue statusObj

                if statusVal = "busy" then
                    let state = fallbackRuntime.GetOrCreateState sid2

                    if state.Lifecycle = FallbackLifecycle.TaskComplete then
                        fallbackRuntime.Update(
                            sid2,
                            fun session ->
                                { session with
                                    Core =
                                        { session.Core with
                                            Lifecycle = FallbackLifecycle.Active } }
                        )

        | Some { EventType = "session.idle"
                 Props = _props } -> ()

        | Some { EventType = "session.compacted"
                 Props = props } ->
            let sid2 = getSessionID "session.compacted" props

            if sid2 <> "" then
                do! handleSessionCompacted ctx fallbackRuntime sid2

        | Some { EventType = "message.updated"
                 Props = props } ->
            let sid2 = getSessionID "message.updated" props

            if sid2 <> "" then
                handleMessageUpdated fallbackRuntime sid2 props

        | _ -> ()
    }

/// True when the envelope is a session.idle or a session.status whose value is "idle".
let private isIdleEnvelope (env: HostEventEnvelope) =
    if env.EventType = "session.idle" then
        true
    elif env.EventType = "session.status" then
        let statusObj = get env.Props "status"
        not (isNullish statusObj) && resolveStatusValue statusObj = "idle"
    else
        false

/// SessionClosed is a single domain command
/// that tears down every per-session side-effect at once,
/// not a "leak one at a time" pattern.  It must be safe
/// to call from the event handler's `finally` because the
/// session is being torn down by the host.  We dispatch
/// it through the unified DispatchRegistry so the
/// per-session mailbox, active dispatch, and pending
/// receipts are all released together.
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
        ChatHooksMessageIdDedup.forget sid

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

            fallback.UpdateBusyCount eventEnvelope
            do! nudge.TrackLifetimeEvents eventEnvelope

            let! fbConsumed = fallback.TryConsumeEvent input

            if fbConsumed then
                if Option.exists isIdleEnvelope eventEnvelope then
                    do! nudge.SettleCompactionIfCompleted sid
                    do! tryIdle (pluginDirectoryFromCtx ctx) sid |> Promise.map ignore
            else
                do! nudge.HandleNaturalStop eventEnvelope

                if Option.exists isIdleEnvelope eventEnvelope then
                    do! tryIdle (pluginDirectoryFromCtx ctx) sid |> Promise.map ignore
        finally
            if sid <> "" then
                fallbackRuntime.Update(sid, setEventHandlingActive false)
                handleSessionClosed ctx sid eventEnvelope
    }
