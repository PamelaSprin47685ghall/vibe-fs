module Wanxiangshu.Hosts.Mux.EventHookHandlers

open Fable.Core
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.ReviewEventWriter
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.RuntimeScopeForgetSession
open Wanxiangshu.Hosts.Mux.EventHookCleanup
open Wanxiangshu.Hosts.Mux.Fallback.Hook
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.SubsessionChildObserver
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Runtime.NudgeRuntimeEvent
open Wanxiangshu.Runtime.NudgeRuntime
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Hosts.Mux.EventHookDecode


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

        let isError = muxEventTranslator.IsSessionError event

        let isIdle =
            muxEventTranslator.IsSessionIdle event
            || (let t = Dyn.str event "type" in t = "session.idle" || t = "stream-end")
            || (let evt = Dyn.get event "event" in

                not (Dyn.isNullish evt)
                && (let t = Dyn.str evt "type" in t = "session.idle" || t = "stream-end"))

        if isError then
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
        elif isIdle then
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

        SubsessionActorRegistry.ClearPoison directory workspaceId
        SubsessionActorRegistry.Remove directory workspaceId
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

        do! runtime.HandleEvent(EventHookDecode.parseHookEvent event, helpers)
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

        let effectiveDir =
            let d = Dyn.str event "workspacePath"

            if d <> "" then
                d
            else
                let d2 = Dyn.str event "directory"

                if d2 <> "" then
                    d2
                else
                    let d3 = Dyn.str helpers "directory"

                    if d3 <> "" then
                        d3
                    else
                        let d4 = Dyn.str helpers "cwd"
                        if d4 <> "" then d4 else directory

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
                    do! handleSessionClosed scope effectiveDir workspaceId

            let isChild =
                workspaceId <> ""
                && SubsessionEventRouter.isChildSession effectiveDir workspaceId

            if isChild then
                do! handleChildSession effectiveDir workspaceId fallbackRuntime event
            else
                match parseHookEvent event with
                | StreamAbort workspaceId
                | AbortedError workspaceId when workspaceId <> "" ->
                    do! handleParentSession scope effectiveDir reviewStore workspaceId event
                | _ -> ()

                let! fbResult = fallbackHandler event

                if not fbResult.Consumed then
                    do! handleUnconsumedEvent effectiveDir workspaceId runtime event helpers
        finally
            if workspaceId <> "" then
                fallbackRuntime.Update(workspaceId, setEventHandlingActive false)
    }
