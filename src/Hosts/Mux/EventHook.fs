module Wanxiangshu.Hosts.Mux.EventHook

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.NudgeRuntime
open Wanxiangshu.Runtime.NudgeRuntimeEvent
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateFlagTransitions
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Hosts.Mux.Fallback.Hook
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.SubsessionChildObserver
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionEventStore

type private DecodedHookEvent =
    { eventType: string
      workspaceId: string
      properties: obj
      stopReason: string
      errorType: string }

let private decodeHookEvent (event: obj) : DecodedHookEvent =
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
let private shouldObserveMuxEvent (eventType: string) : bool =
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

// ARCHITECTURE_EXEMPT: split this 125-line function later
let createEventHook (deps: obj) (reviewStore: ReviewStore) (scope: RuntimeScope) : obj =
    let getChatHistory =
        if Dyn.isNullish deps then
            None
        else
            let getter = Dyn.get deps "getChatHistory"

            if Dyn.isNullish getter then
                None
            else
                Some(fun (workspaceId: string) -> unbox<JS.Promise<obj array>> (Dyn.call1 getter workspaceId))

    let directory = if Dyn.isNullish deps then "" else Dyn.str deps "directory"

    let fallbackRuntime = FallbackRuntimeStore()
    scope.Add("fallbackRuntime", box fallbackRuntime)
    let fallbackConfigOpt = loadFallbackConfig directory

    let isReviewLoopActive (sessionID: string) =
        match reviewStore.getReviewState (sessionID) with
        | Some state -> ReviewSession.StateMachine.isActive state
        | None -> false

    let runtime =
        createNudgeRuntime getChatHistory directory fallbackRuntime isReviewLoopActive

    let configLookup: ConfigLookup =
        match fallbackConfigOpt with
        | Some cfg -> (fun _ -> cfg)
        | None -> (fun _ -> emptyConfig)

    let fallbackHandler =
        createMuxFallbackHandler fallbackRuntime configLookup deps directory

    // ARCHITECTURE_EXEMPT: split this 91-line function later
    let fn =
        // ARCHITECTURE_EXEMPT: split this 88-line function later
        System.Func<obj, obj, JS.Promise<unit>>(fun event helpers ->
            let decoded = decodeHookEvent event

            // Gate: discard streaming/token events synchronously so they
            // never create a Promise closure, SerialQueue entry, or
            // fallback handler invocation.
            if not (shouldObserveMuxEvent decoded.eventType) then
                Promise.lift ()
            else
                promise {
                    let workspaceId = decoded.workspaceId

                    if workspaceId <> "" then
                        fallbackRuntime.SetEventHandlingActive workspaceId true

                    try
                        if workspaceId <> "" then
                            if
                                decoded.eventType = "session.deleted"
                                || decoded.eventType = "session.close"
                                || decoded.eventType = "session.delete"
                                || decoded.eventType = "session.remove"
                            then
                                let sid = SessionId.create workspaceId
                                let eventStore = SubsessionEventStore.create directory
                                do! eventStore.Append(sid, [ PhysicalSessionClosed sid ]) |> Promise.map ignore
                                SubsessionActorRegistry.ClearPoison directory workspaceId
                                SubsessionActorRegistry.Remove directory workspaceId

                        let isChild =
                            workspaceId <> "" && SubsessionEventRouter.isChildSession directory workspaceId

                        if isChild then
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
                        else
                            match parseHookEvent event with
                            | StreamAbort workspaceId
                            | AbortedError workspaceId when workspaceId <> "" ->
                                let root = if directory = "" then workspaceId else directory
                                scope.TriggerInit(root)
                                do! scope.WaitInit()
                                do! appendLoopCancelledOrFail root workspaceId
                                do! syncReviewFromEventLogDedicated reviewStore root workspaceId

                                Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance workspaceId
                                Wanxiangshu.Runtime.ToolHookRuntime.closeSession workspaceId
                                Wanxiangshu.Runtime.RunnerBackground.abortRunnerJobCore scope workspaceId
                            | _ -> ()

                            let! fbResult = fallbackHandler event

                            if not fbResult.Consumed then
                                if SubsessionEventRouter.isChildSession directory workspaceId then
                                    match muxEventTranslator.ExtractTurnObservation event with
                                    | Some obs ->
                                        do!
                                            routeToChild directory workspaceId (EvidenceUpdated obs)
                                            |> Promise.map ignore
                                    | None -> ()

                                do! runtime.HandleEvent(parseHookEvent event, helpers)
                    finally
                        if workspaceId <> "" then
                            fallbackRuntime.SetEventHandlingActive workspaceId false
                })

    box fn
