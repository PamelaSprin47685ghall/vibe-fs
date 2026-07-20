module Wanxiangshu.Hosts.Opencode.Fallback.Hook

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec

module Metadata = Wanxiangshu.Runtime.OpencodeSessionPromptCodec.WanxiangshuMetadataCodec

open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TypeClassify
open Wanxiangshu.Runtime.Fallback.Coordinator
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.SubsessionChildObserver
open Wanxiangshu.Runtime.SubsessionTranscript
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Hosts.Opencode.Fallback.EventTranslator
open Wanxiangshu.Hosts.Opencode.Fallback.MessageInspection
open Wanxiangshu.Hosts.Opencode.Fallback.ActionExecutor

let private setConsumedFromResult
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (result: FallbackHookResult)
    : unit =
    runtime.Update(sessionID, recordConsumed result.Consumed)

let private clearConsumedOnNewUserMessage (runtime: FallbackRuntimeStore) (sessionID: string) (rawEvent: obj) : unit =
    if isNewUserMessageImpl runtime sessionID rawEvent then
        runtime.Update(sessionID, clearConsumption)

let private findAnchorMessageId (msgs: obj array) (turnNonce: string) =
    msgs
    |> Array.tryFindBack (fun msg ->
        if Dyn.isNullish msg then
            false
        else
            let info = Dyn.get msg "info"
            let parts = Dyn.get msg "parts"

            not (Dyn.isNullish info)
            && Dyn.str info "role" = "user"
            && Dyn.isArray parts
            && ((parts :?> obj array)
                |> Array.exists (fun part ->
                    let isText = Dyn.str part "type" = "text"

                    let hasNonce =
                        Metadata.tryDecodeFromPart part |> Option.exists (fun m -> m.Nonce = turnNonce)

                    isText && hasNonce)))
    |> Option.bind (fun msg ->
        let info = Dyn.get msg "info"
        let messageId = Dyn.str msg "id"
        let messageId = if messageId <> "" then messageId else Dyn.str info "id"
        if messageId <> "" then Some messageId else None)

let private refreshChildTurnEvidence
    (workspaceRoot: string)
    (currentClient: unit -> obj)
    (sessionID: string)
    : JS.Promise<unit> =
    promise {
        try
            let arg = box {| path = box {| id = sessionID |} |}
            let! response = invokeClient (currentClient ()) "messages" arg
            let messages = Dyn.get response "data"

            if Dyn.isArray messages then
                let msgs = messages :?> obj array

                match SubsessionActorRegistry.TryGet workspaceRoot sessionID with
                | Some actor ->
                    match actor.GetCurrentTurn() with
                    | Some turnId ->
                        match findAnchorMessageId msgs (TurnId.value turnId) with
                        | Some messageId ->
                            match buildTurnEvidence msgs (AnchorByUserMessageId messageId) with
                            | Ok evidence -> do! routeEvidence workspaceRoot sessionID evidence |> Promise.map ignore
                            | Error _ -> ()
                        | None ->
                            match tryBuildLatestAssistantEvidence msgs with
                            | Some evidence -> do! routeEvidence workspaceRoot sessionID evidence |> Promise.map ignore
                            | None -> ()
                    | None -> ()
                | None -> ()
        with _ ->
            ()
    }

let private getErrorObj (translator: IEventTranslator) (rawEvent: obj) =
    match translator.TranslateError rawEvent with
    | Some(FallbackEvent.SessionError err) -> err
    | _ ->
        { ErrorName = "UnknownError"
          DomainError = None
          Message = "An unknown error occurred"
          StatusCode = None
          IsRetryable = None }

let private handleFallbackEvent
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (refreshChildEvidence: string -> JS.Promise<unit>)
    (baseHandler: obj -> JS.Promise<FallbackHookResult>)
    (rawEvent: obj)
    : JS.Promise<FallbackHookResult> =
    promise {
        let sessionID = translator.ExtractSessionID rawEvent

        let! routed =
            promise {
                if translator.IsSessionError rawEvent then
                    let errorObj = getErrorObj translator rawEvent
                    return! tryError workspaceRoot sessionID errorObj
                elif translator.IsSessionIdle rawEvent then
                    if isChildSession workspaceRoot sessionID then
                        do! refreshChildEvidence sessionID

                    return! tryIdle workspaceRoot sessionID
                elif isChildSession workspaceRoot sessionID then
                    match translator.ExtractTurnObservation rawEvent with
                    | Some obs -> do! routeToChild workspaceRoot sessionID (EvidenceUpdated obs) |> Promise.map ignore
                    | None -> ()

                    return absorbChildMetadata workspaceRoot runtime sessionID rawEvent
                else
                    return false
            }

        if routed then
            return
                { Consumed = true
                  State = runtime.GetOrCreateState sessionID }
        else
            let! result = baseHandler rawEvent
            setConsumedFromResult runtime sessionID result
            clearConsumedOnNewUserMessage runtime sessionID rawEvent
            return result
    }

let createOpencodeFallbackHandler
    (client: obj)
    (runtime: FallbackRuntimeStore)
    (configLookup: ConfigLookup)
    (workspaceRoot: string)
    (_registry: ChildAgentRegistry)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (clientContext: obj option)
    : (obj -> JS.Promise<FallbackHookResult>) =
    let translator = opencodeEventTranslator runtime

    let currentClient () =
        match clientContext with
        | Some context ->
            match getClientFromPluginCtx context with
            | Ok current -> current
            | Error _ -> client
        | None -> client

    let refreshChildEvidence sid =
        refreshChildTurnEvidence workspaceRoot currentClient sid

    let pendingReview sid =
        reviewStore.getPendingReviewIds () |> List.contains sid

    let baseHandler =
        createHandler
            translator
            runtime
            configLookup
            (opencodeActionExecutorWithDir runtime client workspaceRoot)
            workspaceRoot
            (Some pendingReview)

    fun rawEvent -> handleFallbackEvent translator runtime workspaceRoot refreshChildEvidence baseHandler rawEvent
