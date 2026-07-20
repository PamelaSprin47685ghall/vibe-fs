module Wanxiangshu.Hosts.Omp.Fallback.Hook

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TypeClassify
open Wanxiangshu.Runtime.Fallback.Coordinator
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.SubsessionChildObserver
open Wanxiangshu.Hosts.Omp.Fallback.EventTranslator
open Wanxiangshu.Hosts.Omp.Fallback.ActionExecutor

let private setConsumedFromResult
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (result: FallbackHookResult)
    : unit =
    runtime.Update(sessionID, recordConsumed result.Consumed)

let private clearConsumedOnNewUserMessage
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (rawEvent: obj)
    : unit =
    if translator.IsNewUserMessage(sessionID, rawEvent) then
        runtime.Update(sessionID, clearConsumption)

let private bindAgentAndModel (runtime: FallbackRuntimeStore) (rawEvent: obj) : unit =
    let eventObj = Dyn.get rawEvent "event"
    let eventType = Dyn.str eventObj "type"

    if eventType = "session.busy" || eventType = "session.updated" then
        let info = Dyn.get eventObj "info"

        if not (Dyn.isNullish info) then
            let sid = Dyn.str (Dyn.get rawEvent "props") "sessionID"

            if sid <> "" then
                let agent = Dyn.str info "agent"

                if agent <> "" then
                    runtime.UpdateSession(sid, recordAgentName agent)

                let modelObj = Dyn.get info "model"

                match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.decodeModelFromObj modelObj with
                | Some m -> runtime.UpdateSession(sid, selectModel m)
                | None -> ()

let private routeEvent
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (rawEvent: obj)
    : JS.Promise<bool> =
    promise {
        if translator.IsSessionError rawEvent then
            let errorObj =
                match translator.TranslateError rawEvent with
                | Some(FallbackEvent.SessionError err) -> err
                | _ ->
                    { ErrorName = "UnknownError"
                      DomainError = None
                      Message = "An unknown error occurred"
                      StatusCode = None
                      IsRetryable = None }

            return! tryError workspaceRoot sessionID errorObj
        elif translator.IsSessionIdle rawEvent then
            let ws = Wanxiangshu.Hosts.Omp.OmpSubsessionHostHelper.workspaceFor workspaceRoot
            let isNewIdle =
                match Wanxiangshu.Runtime.SubsessionActorRegistry.SubsessionActorRegistry.TryGet workspaceRoot sessionID with
                | Some actor ->
                    match actor.GetCurrentTurn() with
                    | Some turnId ->
                        match Wanxiangshu.Runtime.Dispatch.HostReceiptWaiterRegistry.tryFind ws sessionID (TurnId.value turnId) with
                        | Some waiter -> waiter.Completed
                        | None -> false
                    | None -> false
                | None -> false
            if isNewIdle then
                return! tryIdle workspaceRoot sessionID
            else
                return false
        elif isChildSession workspaceRoot sessionID then
            match translator.ExtractTurnObservation rawEvent with
            | Some obs -> do! routeToChild workspaceRoot sessionID (EvidenceUpdated obs) |> Promise.map ignore
            | None -> ()

            return absorbChildMetadata workspaceRoot runtime sessionID rawEvent
        else
            return false
    }

let createOmpFallbackHandler
    (runtime: FallbackRuntimeStore)
    (configLookup: ConfigLookup)
    (sessionApi: obj)
    (workspaceRoot: string)
    : (obj -> JS.Promise<FallbackHookResult>) =
    let translator = ompEventTranslator runtime

    let baseHandler =
        createHandler translator runtime configLookup (ompActionExecutor runtime sessionApi) workspaceRoot None

    fun (rawEvent: obj) ->
        promise {
            let sessionID = translator.ExtractSessionID rawEvent
            let! routed = routeEvent translator runtime workspaceRoot sessionID rawEvent

            if routed then
                return
                    { Consumed = true
                      State = runtime.GetOrCreateState sessionID }
            else
                bindAgentAndModel runtime rawEvent
                let! result = baseHandler rawEvent
                setConsumedFromResult runtime sessionID result
                clearConsumedOnNewUserMessage translator runtime sessionID rawEvent
                return result
        }
