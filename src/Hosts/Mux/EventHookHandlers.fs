module Wanxiangshu.Hosts.Mux.EventHookHandlers

open Fable.Core
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.RuntimeScopeForgetSession
open Wanxiangshu.Hosts.Mux.Fallback.Hook
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.SubsessionChildObserver
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Runtime.NudgeRuntimeEvent
open Wanxiangshu.Runtime.NudgeRuntime
open Wanxiangshu.Kernel.FallbackKernel.Types

type DecodedHookEvent =
    { eventType: string
      workspaceId: string
      properties: obj
      stopReason: string
      errorType: string }

let decodeHookEvent (event: obj) : DecodedHookEvent =
    let props = Dyn.get event "properties"

    let meta =
        if Dyn.isNullish props then
            null
        else
            Dyn.get props "metadata"

    { eventType = if Dyn.isNullish event then "" else Dyn.str event "type"
      workspaceId = Dyn.str event "workspaceId"
      properties = if Dyn.isNullish props then null else props
      stopReason =
        if Dyn.isNullish meta then
            ""
        else
            Dyn.str meta "muxStopReason"
      errorType =
        if Dyn.isNullish props then
            ""
        else
            Dyn.str props "errorType" }

let private getLastAssistantText (properties: obj) : string =
    if Dyn.isNullish properties then
        ""
    else
        let parts = Dyn.get properties "parts"

        if Dyn.isNullish parts || not (Dyn.isArray parts) then
            ""
        else
            (parts :?> obj array)
            |> Array.filter (fun p -> Dyn.str p "type" = "text")
            |> Array.map (fun p -> Dyn.str p "text")
            |> String.concat "\n"

let private parseHookEvent (event: obj) : NudgeRuntimeEvent =
    let decoded = decodeHookEvent event

    if decoded.workspaceId = "" then
        Ignore
    else
        match decoded.eventType with
        | "stream-end" -> StreamEnd(decoded.workspaceId, decoded.stopReason, getLastAssistantText decoded.properties)
        | "stream-abort" -> StreamAbort decoded.workspaceId
        | "error" when decoded.errorType = "aborted" -> AbortedError decoded.workspaceId
        | _ -> Ignore

/// Only terminal / lifecycle events should enter the Promise machinery.
/// Every streaming chunk (message.updated, message.part.*, stream-* mid-flow)
/// is discarded synchronously here so the closure never captures a rawEvent.
let shouldObserveMuxEvent (eventType: string) : bool =
    match eventType with
    | "stream-end"
    | "stream-abort"
    | "error"
    | "session.error"
    | "session.deleted"
    | "session.close"
    | "session.delete"
    | "session.remove" -> true
    | _ -> false

// ---------------------------------------------------------------------------
// Extracted helpers for session-closed lifecycle events
// ---------------------------------------------------------------------------

let handleSessionClosed
    (scope: RuntimeScope)
    (directory: string)
    (workspaceId: string)
    (event: obj)
    : JS.Promise<unit> =
    promise {
        forgetSession scope workspaceId
        let sid = SessionId.create workspaceId
        let eventStore = SubsessionEventStore.create directory
        do! eventStore.Append(sid, [ PhysicalSessionClosed sid ]) |> Promise.map ignore
        SubsessionActorRegistry.ClearPoison directory workspaceId
        SubsessionActorRegistry.Remove directory workspaceId
        Wanxiangshu.Runtime.SubsessionPendingEvidence.SubsessionPendingEvidence.ForgetSession workspaceId
    }

// ---------------------------------------------------------------------------
// Extracted helpers for child-session routing
// ---------------------------------------------------------------------------

let handleChildSession
    (directory: string)
    (workspaceId: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (event: obj)
    : JS.Promise<unit> =
    promise {
        SubsessionChildObserver.observeChildMetadata fallbackRuntime workspaceId event

        match muxEventTranslator.ExtractTurnObservation event with
        | Some obs ->
            let! _ = SubsessionEventRouter.routeToChild directory workspaceId (EvidenceUpdated obs)
            ()
        | None -> ()

        if muxEventTranslator.IsSessionError event then
            let errorObj =
                match muxEventTranslator.TranslateError event with
                | Some(FallbackEvent.SessionError err) -> err
                | _ ->
                    { ErrorName = "UnknownError"
                      DomainError = None
                      Message = "An unknown error occurred"
                      StatusCode = None
                      IsRetryable = None }

            let! _ = SubsessionEventRouter.tryError directory workspaceId errorObj
            ()
        elif muxEventTranslator.IsSessionIdle event then
            let! _ = SubsessionEventRouter.tryIdle directory workspaceId
            ()
    }

// ---------------------------------------------------------------------------
// Extracted helpers for parent-session abort/error lifecycle
// ---------------------------------------------------------------------------

let handleParentSession
    (scope: RuntimeScope)
    (directory: string)
    (reviewStore: ReviewStore)
    (workspaceId: string)
    (event: obj)
    : JS.Promise<unit> =
    promise {
        let root = if directory = "" then workspaceId else directory
        scope.TriggerInit(root)
        do! scope.WaitInit()
        do! appendLoopCancelledOrFail root workspaceId
        do! syncReviewFromEventLogDedicated reviewStore root workspaceId

        Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance workspaceId
        Wanxiangshu.Runtime.ToolHookRuntime.closeSession workspaceId
        Wanxiangshu.Runtime.RunnerBackground.abortRunnerJobCore scope workspaceId
        forgetSession scope workspaceId
    }

// ---------------------------------------------------------------------------
// Extracted helper for unconsumed fallback result
// ---------------------------------------------------------------------------

let handleUnconsumedEvent
    (directory: string)
    (workspaceId: string)
    (runtime: NudgeRuntime)
    (event: obj)
    (helpers: obj)
    : JS.Promise<unit> =
    promise {
        if SubsessionEventRouter.isChildSession directory workspaceId then
            match muxEventTranslator.ExtractTurnObservation event with
            | Some obs ->
                do!
                    SubsessionEventRouter.routeToChild directory workspaceId (EvidenceUpdated obs)
                    |> Promise.map ignore
            | None -> ()

        do! runtime.HandleEvent(parseHookEvent event, helpers)
    }

// ---------------------------------------------------------------------------
// Top-level extracted event-processing body (replaces inline fn lambda)
// ---------------------------------------------------------------------------

let processMuxEvent
    (decoded: DecodedHookEvent)
    (fallbackRuntime: FallbackRuntimeStore)
    (directory: string)
    (scope: RuntimeScope)
    (reviewStore: ReviewStore)
    (runtime: NudgeRuntime)
    (fallbackHandler: obj -> JS.Promise<FallbackHookResult>)
    (event: obj)
    (helpers: obj)
    : JS.Promise<unit> =
    promise {
        let workspaceId = decoded.workspaceId

        if workspaceId <> "" then
            fallbackRuntime.Update(workspaceId, setEventHandlingActive true)

        try
            if workspaceId <> "" then
                if
                    decoded.eventType = "session.deleted"
                    || decoded.eventType = "session.close"
                    || decoded.eventType = "session.delete"
                    || decoded.eventType = "session.remove"
                then
                    do! handleSessionClosed scope directory workspaceId event

            let isChild =
                workspaceId <> "" && SubsessionEventRouter.isChildSession directory workspaceId

            if isChild then
                do! handleChildSession directory workspaceId fallbackRuntime event
            else
                match parseHookEvent event with
                | StreamAbort workspaceId
                | AbortedError workspaceId when workspaceId <> "" ->
                    do! handleParentSession scope directory reviewStore workspaceId event
                | _ -> ()

                let! fbResult = fallbackHandler event

                if not fbResult.Consumed then
                    do! handleUnconsumedEvent directory workspaceId runtime event helpers
        finally
            if workspaceId <> "" then
                fallbackRuntime.Update(workspaceId, setEventHandlingActive false)
    }
