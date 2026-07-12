module Wanxiangshu.Shell.FallbackEventBridge

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.StateMachine
open Wanxiangshu.Shell.FallbackMessageCodec
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackConfigCodec
open Wanxiangshu.Shell.PromiseQueue
open Wanxiangshu.Shell.EventLogRuntimeAppend
open Wanxiangshu.Shell.Clock

// ---------------------------------------------------------------------------
// Host-facing interfaces
// ---------------------------------------------------------------------------

type IEventTranslator =
    abstract TranslateError: obj -> FallbackEvent option
    abstract ExtractSessionID: obj -> string
    abstract IsSessionError: obj -> bool
    abstract IsSessionIdle: obj -> bool
    abstract IsSessionBusy: obj -> bool
    abstract IsNewUserMessage: sessionID: string * rawEvent: obj -> bool
    abstract ExtractRoutingContext: rawEvent: obj -> (string option * string option)

type IActionExecutor =
    abstract SendContinue: sessionID: string * model: FallbackModel * continuationID: string -> JS.Promise<unit>
    abstract FetchMessages: sessionID: string -> JS.Promise<obj array>
    abstract PropagateFailure: sessionID: string -> JS.Promise<unit>
    abstract CaptureCurrentModel: sessionID: string -> JS.Promise<FallbackModel option>

    abstract RecoverWithPrompt:
        sessionID: string * model: FallbackModel * promptText: string * continuationID: string -> JS.Promise<unit>

    abstract AbortRun: sessionID: string -> JS.Promise<unit>

type ConfigLookup = (string -> FallbackConfig)

// ---------------------------------------------------------------------------
// Core handler
// ---------------------------------------------------------------------------

type ContinuationIntent =
    | SendContinueIntent of
        model: FallbackModel *
        agent: string *
        turnId: string *
        gen: int *
        cancelGen: int *
        continuationID: string
    | RecoverWithPromptIntent of
        model: FallbackModel *
        promptText: string *
        agent: string *
        turnId: string *
        gen: int *
        cancelGen: int *
        continuationID: string
    | PropagateFailureIntent

let verifyLeaseWithStatus
    (expectedStatus: string)
    (runtime: FallbackRuntimeState)
    (sessionID: string)
    (lease: PendingLease)
    : bool =
    let currentGen = runtime.GetSessionGeneration sessionID
    let currentCancelGen = runtime.GetCancelGeneration sessionID
    let currentTurnId = runtime.GetHumanTurnId sessionID
    let currentOwner = runtime.GetSessionOwner sessionID
    let stateOpt = runtime.TryGetState sessionID

    let matches =
        lease.SessionGeneration = currentGen
        && lease.HumanTurnID = currentTurnId
        && lease.CancelGeneration = currentCancelGen
        && currentOwner = "Fallback"
        && not (runtime.IsForceStopped sessionID)
        && (match stateOpt with
            | Some s -> s.Lifecycle = FallbackLifecycle.Active
            | None -> false)
        && (match runtime.TryGetPendingLease sessionID with
            | Some pending -> pending.ContinuationID = lease.ContinuationID && pending.Status = expectedStatus
            | None -> false)

    matches

let verifyLease (runtime: FallbackRuntimeState) (sessionID: string) (lease: PendingLease) : bool =
    verifyLeaseWithStatus "requested" runtime sessionID lease

let executeContinuationIntent
    (runtime: FallbackRuntimeState)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (intent: ContinuationIntent)
    : JS.Promise<unit> =
    promise {
        match intent with
        | SendContinueIntent(model, agent, turnId, gen, cancelGen, continuationID) ->
            let lease =
                { ContinuationID = continuationID
                  SessionGeneration = gen
                  HumanTurnID = turnId
                  CancelGeneration = cancelGen
                  Owner = "Fallback"
                  Model = model
                  PromptText = None
                  Status = "requested" }

            if verifyLease runtime sessionID lease then
                let startedLease =
                    { lease with
                        Status = "dispatch_started" }

                runtime.SetPendingLease(sessionID, startedLease)
                do! appendContinuationDispatchStartedOrFail workspaceRoot sessionID continuationID

                try
                    do! executor.SendContinue(sessionID, model, continuationID)

                    let isValid = verifyLeaseWithStatus "dispatch_started" runtime sessionID lease

                    if not isValid then
                        do! executor.AbortRun sessionID
                        let cancelledLease = { lease with Status = "cancelled" }
                        runtime.SetPendingLease(sessionID, cancelledLease)

                        do!
                            appendContinuationCancelledOrFail
                                workspaceRoot
                                sessionID
                                continuationID
                                "Cancelled after dispatch"
                    else
                        let dispatchedLease = { lease with Status = "dispatched" }
                        runtime.SetPendingLease(sessionID, dispatchedLease)

                        let modelStr =
                            match model.Variant with
                            | Some v -> model.ProviderID + "/" + model.ModelID + ":" + v
                            | None -> model.ProviderID + "/" + model.ModelID

                        let atMs = getTimestampMs ()
                        runtime.SetInjectedAt sessionID atMs
                        runtime.SetInjectedModel sessionID model

                        do!
                            appendContinuationDispatchedOrFail
                                workspaceRoot
                                sessionID
                                continuationID
                                modelStr
                                agent
                                atMs
                with ex ->
                    let failedLease = { lease with Status = "failed" }
                    runtime.SetPendingLease(sessionID, failedLease)
                    do! appendContinuationFailedOrFail workspaceRoot sessionID continuationID ex.Message
            else
                let cancelledLease = { lease with Status = "cancelled" }
                runtime.SetPendingLease(sessionID, cancelledLease)
                do! appendContinuationCancelledOrFail workspaceRoot sessionID continuationID "Lease validation failed"

        | RecoverWithPromptIntent(model, promptText, agent, turnId, gen, cancelGen, continuationID) ->
            let lease =
                { ContinuationID = continuationID
                  SessionGeneration = gen
                  HumanTurnID = turnId
                  CancelGeneration = cancelGen
                  Owner = "Fallback"
                  Model = model
                  PromptText = Some promptText
                  Status = "requested" }

            if verifyLease runtime sessionID lease then
                let startedLease =
                    { lease with
                        Status = "dispatch_started" }

                runtime.SetPendingLease(sessionID, startedLease)
                do! appendContinuationDispatchStartedOrFail workspaceRoot sessionID continuationID

                try
                    do! executor.RecoverWithPrompt(sessionID, model, promptText, continuationID)

                    let isValid = verifyLeaseWithStatus "dispatch_started" runtime sessionID lease

                    if not isValid then
                        do! executor.AbortRun sessionID
                        let cancelledLease = { lease with Status = "cancelled" }
                        runtime.SetPendingLease(sessionID, cancelledLease)

                        do!
                            appendContinuationCancelledOrFail
                                workspaceRoot
                                sessionID
                                continuationID
                                "Cancelled after dispatch"
                    else
                        let dispatchedLease = { lease with Status = "dispatched" }
                        runtime.SetPendingLease(sessionID, dispatchedLease)

                        let modelStr =
                            match model.Variant with
                            | Some v -> model.ProviderID + "/" + model.ModelID + ":" + v
                            | None -> model.ProviderID + "/" + model.ModelID

                        let atMs = getTimestampMs ()

                        do!
                            appendContinuationDispatchedOrFail
                                workspaceRoot
                                sessionID
                                continuationID
                                modelStr
                                agent
                                atMs
                with ex ->
                    let failedLease = { lease with Status = "failed" }
                    runtime.SetPendingLease(sessionID, failedLease)
                    do! appendContinuationFailedOrFail workspaceRoot sessionID continuationID ex.Message
            else
                let cancelledLease = { lease with Status = "cancelled" }
                runtime.SetPendingLease(sessionID, cancelledLease)
                do! appendContinuationCancelledOrFail workspaceRoot sessionID continuationID "Lease validation failed"

        | PropagateFailureIntent -> do! executor.PropagateFailure sessionID
    }

let handleEvent
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeState)
    (configLookup: ConfigLookup)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (rawEvent: obj)
    (pendingReview: (string -> bool) option)
    : JS.Promise<FallbackHookResult * ContinuationIntent option> =

    promise {
        let sessionID = translator.ExtractSessionID rawEvent

        if
            translator.IsSessionBusy rawEvent
            || translator.IsSessionIdle rawEvent
            || translator.IsSessionError rawEvent
        then
            runtime.SetAwaitingBusy sessionID false

        let! eventOpt =
            match translator.TranslateError rawEvent with
            | Some _ as ev -> promise { return ev }
            | None ->
                if translator.IsNewUserMessage(sessionID, rawEvent) then
                    promise { return Some FallbackEvent.NewUserMessage }
                elif translator.IsSessionBusy rawEvent then
                    promise { return Some FallbackEvent.SessionBusy }
                elif translator.IsSessionIdle rawEvent then
                    promise {
                        let state = runtime.GetOrCreateState sessionID

                        if state.Lifecycle = FallbackLifecycle.Cancelled then
                            return Some FallbackEvent.SessionIdle
                        else
                            let! msgs = executor.FetchMessages sessionID

                            match FallbackMessageCodec.tryGetLastAssistantAbortInfo msgs with
                            | Some abortErr -> return Some(FallbackEvent.SessionError abortErr)
                            | None ->
                                if isIdleNoContentAndNoTools msgs then
                                    if pendingReview |> Option.exists (fun f -> f sessionID) then
                                        return Some FallbackEvent.SessionIdle
                                    else
                                        return
                                            Some(
                                                FallbackEvent.SessionError
                                                    { ErrorName = "EmptyOutputError"
                                                      DomainError = None
                                                      Message = "LLM returned empty output without tools"
                                                      StatusCode = None
                                                      IsRetryable = Some true }
                                            )
                                else
                                    return Some FallbackEvent.SessionIdle
                    }
                else
                    promise { return None }

        let isStale =
            match eventOpt with
            | None -> false
            | Some evt ->
                if evt = FallbackEvent.NewUserMessage then
                    false
                else
                    let isAbortError =
                        match evt with
                        | FallbackEvent.SessionError err when
                            Wanxiangshu.Kernel.FallbackKernel.Decision.errorInputIsAbort err
                            ->
                            true
                        | _ -> false

                    if isAbortError then
                        let props = Dyn.get rawEvent "properties"
                        let props = if Dyn.isNullish props then rawEvent else props
                        let info = Dyn.get props "info"
                        let info = if Dyn.isNullish info then props else info

                        let eventTurnId =
                            let tid = Dyn.str info "turnId"
                            let tid = if tid <> "" then tid else Dyn.str info "turnID"
                            let tid = if tid <> "" then tid else Dyn.str info "runId"
                            let tid = if tid <> "" then tid else Dyn.str info "runID"
                            tid

                        if eventTurnId <> "" && eventTurnId <> runtime.GetHumanTurnId sessionID then
                            true
                        else
                            let activeGen = runtime.GetActiveContinuationGeneration sessionID
                            let activeCancel = runtime.GetActiveContinuationCancelGeneration sessionID
                            let currentGen = runtime.GetSessionGeneration sessionID
                            let currentCancel = runtime.GetCancelGeneration sessionID
                            activeGen < currentGen || activeCancel < currentCancel
                    else
                        let state = runtime.GetOrCreateState sessionID

                        state.Lifecycle = FallbackLifecycle.Cancelled
                        || (let activeGen = runtime.GetActiveContinuationGeneration sessionID
                            let activeCancel = runtime.GetActiveContinuationCancelGeneration sessionID
                            let currentGen = runtime.GetSessionGeneration sessionID
                            let currentCancel = runtime.GetCancelGeneration sessionID
                            activeGen < currentGen || activeCancel < currentCancel)

        let eventOpt = if isStale then None else eventOpt

        match eventOpt with
        | None ->
            return
                { Consumed = false
                  State = runtime.GetOrCreateState sessionID },
                None

        | Some evt ->
            if evt = FallbackEvent.NewUserMessage then
                if runtime.GetSessionOwner sessionID = "Compaction" then
                    let activeComp = runtime.GetActiveCompactionId sessionID
                    do! appendCompactionSettledOrFail workspaceRoot sessionID activeComp "cancelled"
                    runtime.SetSessionOwner sessionID "None"

                runtime.SetChain sessionID []
                runtime.ClearModel sessionID
                runtime.ClearInjected sessionID
                runtime.SetSessionOwner sessionID "Human"
                let turnId = runtime.IncrementHumanTurnId sessionID
                runtime.RemoveForceStopped sessionID

                let currentGen = runtime.GetSessionGeneration sessionID
                let currentCancelGen = runtime.GetCancelGeneration sessionID
                runtime.SetActiveContinuationGeneration sessionID currentGen
                runtime.SetActiveContinuationCancelGeneration sessionID currentCancelGen

                let modelOpt, agentOpt = translator.ExtractRoutingContext rawEvent

                match modelOpt with
                | Some m -> runtime.SetLatestHumanModel sessionID m
                | None -> runtime.ClearLatestHumanModel sessionID

                match agentOpt with
                | Some a -> runtime.SetAgentName sessionID a
                | None -> ()

                let provider, model, variant =
                    match modelOpt with
                    | Some m ->
                        match decodeModelFromObj (box m) with
                        | Some modelObj ->
                            modelObj.ProviderID, modelObj.ModelID, (modelObj.Variant |> Option.defaultValue "")
                        | None -> "", m, ""
                    | None -> "", "", ""

                let agent = agentOpt |> Option.defaultValue ""

                do! appendHumanTurnStartedOrFail workspaceRoot sessionID turnId provider model variant agent

                let state = runtime.GetOrCreateState sessionID
                let agentName = runtime.GetAgentName sessionID
                let cfg = configLookup agentName
                let ns, _ = transition state evt cfg []
                runtime.UpdateState sessionID ns
                runtime.SetConsumed sessionID false
                return { Consumed = false; State = ns }, None
            else
                let state = runtime.GetOrCreateState sessionID

                let agentName = runtime.GetAgentName sessionID
                let cfg = configLookup agentName

                let! chain =
                    promise {
                        let existing = runtime.GetChain sessionID

                        if not (List.isEmpty existing) then
                            return existing
                        else
                            let! currentModel = executor.CaptureCurrentModel sessionID

                            let resolved =
                                Map.tryFind (normalizeAgentName agentName) cfg.AgentChains
                                |> Option.defaultValue cfg.DefaultChain

                            let finalChain =
                                match currentModel with
                                | Some current ->
                                    match resolved with
                                    | first :: _ when
                                        first.ProviderID = current.ProviderID && first.ModelID = current.ModelID
                                        ->
                                        resolved
                                    | _ ->
                                        let filtered =
                                            resolved
                                            |> List.filter (fun m ->
                                                m.ProviderID <> current.ProviderID || m.ModelID <> current.ModelID)

                                        current :: filtered
                                | None -> resolved

                            if not (List.isEmpty finalChain) then
                                runtime.SetChain sessionID finalChain
                                return finalChain
                            else
                                return []
                    }

                if List.isEmpty chain then
                    return { Consumed = false; State = state }, None
                else
                    let ns, action = transition state evt cfg chain
                    runtime.UpdateState sessionID ns

                    let isAborting =
                        match evt with
                        | FallbackEvent.SessionError err when
                            Wanxiangshu.Kernel.FallbackKernel.Decision.errorInputIsAbort err
                            ->
                            true
                        | _ -> false

                    if isAborting then
                        do! appendUserAbortObservedOrFail workspaceRoot sessionID
                        let _ = runtime.IncrementCancelGeneration sessionID

                        match runtime.TryGetPendingLease sessionID with
                        | Some lease ->
                            let cancelledLease = { lease with Status = "cancelled" }
                            runtime.SetPendingLease(sessionID, cancelledLease)

                            do!
                                appendContinuationCancelledOrFail
                                    workspaceRoot
                                    sessionID
                                    lease.ContinuationID
                                    "User aborted"
                        | None -> ()

                    let mutable finalState = ns

                    let terminalStates =
                        ns.Lifecycle = FallbackLifecycle.TaskComplete
                        || ns.Lifecycle = FallbackLifecycle.Cancelled
                        || ns.Phase = FallbackPhase.Exhausted
                        || (ns.Phase = FallbackPhase.Idle && action = FallbackAction.DoNothing)

                    if terminalStates then
                        // runtime.SetSessionOwner sessionID "None"
                        ()

                    let! (finalState2, intentOpt) =
                        promise {
                            match action with
                            | FallbackAction.DoNothing -> return finalState, None
                            | FallbackAction.SendContinue model ->
                                runtime.SetSessionOwner sessionID "Fallback"
                                let atMs = getTimestampMs ()
                                let agent = runtime.GetAgentName sessionID

                                let modelStr =
                                    match model.Variant with
                                    | Some v -> model.ProviderID + "/" + model.ModelID + ":" + v
                                    | None -> model.ProviderID + "/" + model.ModelID

                                runtime.SetAwaitingBusy sessionID true
                                let currentGen = runtime.GetSessionGeneration sessionID
                                let currentCancelGen = runtime.GetCancelGeneration sessionID
                                runtime.SetActiveContinuationGeneration sessionID currentGen
                                runtime.SetActiveContinuationCancelGeneration sessionID currentCancelGen

                                let continuationID = System.Guid.NewGuid().ToString("N")

                                let lease =
                                    { ContinuationID = continuationID
                                      SessionGeneration = currentGen
                                      HumanTurnID = runtime.GetHumanTurnId sessionID
                                      CancelGeneration = currentCancelGen
                                      Owner = "Fallback"
                                      Model = model
                                      PromptText = None
                                      Status = "requested" }

                                runtime.SetPendingLease(sessionID, lease)

                                do!
                                    appendContinuationRequestedOrFail
                                        workspaceRoot
                                        sessionID
                                        continuationID
                                        modelStr
                                        agent
                                        atMs
                                        currentGen
                                        currentCancelGen
                                        (runtime.GetHumanTurnId sessionID)
                                        "Fallback"

                                let intent =
                                    SendContinueIntent(
                                        model,
                                        agent,
                                        runtime.GetHumanTurnId sessionID,
                                        currentGen,
                                        currentCancelGen,
                                        continuationID
                                    )

                                return finalState, Some intent

                            | FallbackAction.RecoverWithPrompt(model, promptText) ->
                                runtime.SetSessionOwner sessionID "Fallback"
                                runtime.SetAwaitingBusy sessionID true
                                let currentGen = runtime.GetSessionGeneration sessionID
                                let currentCancelGen = runtime.GetCancelGeneration sessionID
                                runtime.SetActiveContinuationGeneration sessionID currentGen
                                runtime.SetActiveContinuationCancelGeneration sessionID currentCancelGen

                                let continuationID = System.Guid.NewGuid().ToString("N")

                                let lease =
                                    { ContinuationID = continuationID
                                      SessionGeneration = currentGen
                                      HumanTurnID = runtime.GetHumanTurnId sessionID
                                      CancelGeneration = currentCancelGen
                                      Owner = "Fallback"
                                      Model = model
                                      PromptText = Some promptText
                                      Status = "requested" }

                                runtime.SetPendingLease(sessionID, lease)

                                let agent = runtime.GetAgentName sessionID

                                let modelStr =
                                    match model.Variant with
                                    | Some v -> model.ProviderID + "/" + model.ModelID + ":" + v
                                    | None -> model.ProviderID + "/" + model.ModelID

                                do!
                                    appendContinuationRequestedOrFail
                                        workspaceRoot
                                        sessionID
                                        continuationID
                                        modelStr
                                        agent
                                        (getTimestampMs ())
                                        currentGen
                                        currentCancelGen
                                        (runtime.GetHumanTurnId sessionID)
                                        "Fallback"

                                let intent =
                                    RecoverWithPromptIntent(
                                        model,
                                        promptText,
                                        agent,
                                        runtime.GetHumanTurnId sessionID,
                                        currentGen,
                                        currentCancelGen,
                                        continuationID
                                    )

                                return finalState, Some intent

                            | FallbackAction.ScanToolCallAsText ->
                                let! msgs = executor.FetchMessages sessionID

                                if allTodosCompleted msgs then
                                    let updated =
                                        { ns with
                                            Phase = FallbackPhase.Idle
                                            Lifecycle = FallbackLifecycle.TaskComplete }

                                    runtime.UpdateState sessionID updated
                                    // runtime.SetSessionOwner sessionID "None"
                                    return updated, None
                                else
                                    match FallbackMessageCodec.scanToolCallAsText msgs with
                                    | Some promptText ->
                                        match List.tryItem ns.CurrentIndex chain with
                                        | Some model ->
                                            let updated =
                                                { ns with
                                                    Phase = FallbackPhase.RecoveringToolCallText }

                                            runtime.UpdateState sessionID updated
                                            runtime.SetAwaitingBusy sessionID true
                                            let currentGen = runtime.GetSessionGeneration sessionID
                                            let currentCancelGen = runtime.GetCancelGeneration sessionID
                                            runtime.SetActiveContinuationGeneration sessionID currentGen
                                            runtime.SetActiveContinuationCancelGeneration sessionID currentCancelGen
                                            runtime.SetSessionOwner sessionID "Fallback"

                                            let continuationID = System.Guid.NewGuid().ToString("N")

                                            let lease =
                                                { ContinuationID = continuationID
                                                  SessionGeneration = currentGen
                                                  HumanTurnID = runtime.GetHumanTurnId sessionID
                                                  CancelGeneration = currentCancelGen
                                                  Owner = "Fallback"
                                                  Model = model
                                                  PromptText = Some promptText
                                                  Status = "requested" }

                                            runtime.SetPendingLease(sessionID, lease)

                                            let agent = runtime.GetAgentName sessionID

                                            let modelStr =
                                                match model.Variant with
                                                | Some v -> model.ProviderID + "/" + model.ModelID + ":" + v
                                                | None -> model.ProviderID + "/" + model.ModelID

                                            do!
                                                appendContinuationRequestedOrFail
                                                    workspaceRoot
                                                    sessionID
                                                    continuationID
                                                    modelStr
                                                    agent
                                                    (getTimestampMs ())
                                                    currentGen
                                                    currentCancelGen
                                                    (runtime.GetHumanTurnId sessionID)
                                                    "Fallback"

                                            let intent =
                                                RecoverWithPromptIntent(
                                                    model,
                                                    promptText,
                                                    agent,
                                                    runtime.GetHumanTurnId sessionID,
                                                    currentGen,
                                                    currentCancelGen,
                                                    continuationID
                                                )

                                            return updated, Some intent
                                        | None ->
                                            let updated = { ns with Phase = FallbackPhase.Idle }
                                            runtime.UpdateState sessionID updated
                                            // runtime.SetSessionOwner sessionID "None"
                                            return updated, None
                                    | None ->
                                        let isToolFinish = FallbackMessageCodec.isLastAssistantToolFinish msgs
                                        let hasResult = FallbackMessageCodec.hasToolResultAfter msgs
                                        let taskComplete = (not isToolFinish) || hasResult

                                        let updated =
                                            { ns with
                                                Phase = FallbackPhase.Idle
                                                Lifecycle =
                                                    (if taskComplete then
                                                         FallbackLifecycle.TaskComplete
                                                     else
                                                         FallbackLifecycle.Active) }

                                        runtime.UpdateState sessionID updated

                                        if updated.Lifecycle = FallbackLifecycle.TaskComplete then
                                            // runtime.SetSessionOwner sessionID "None"
                                            ()

                                        return updated, None
                            | FallbackAction.PropagateFailure -> return finalState, Some PropagateFailureIntent
                        }

                    let consumed =
                        match evt with
                        | FallbackEvent.SessionError _ ->
                            match finalState2.Phase with
                            | FallbackPhase.Exhausted -> false
                            | _ -> true
                        | FallbackEvent.SessionIdle ->
                            match finalState2.Phase with
                            | FallbackPhase.ScanningToolCallText
                            | FallbackPhase.RecoveringToolCallText -> true
                            | _ -> false
                        | FallbackEvent.SessionBusy ->
                            match state.Phase with
                            | FallbackPhase.Retrying _
                            | FallbackPhase.Scanning _ -> true
                            | _ -> false
                        | _ -> false

                    runtime.SetConsumed sessionID consumed

                    return
                        { Consumed = consumed
                          State = finalState2 },
                        intentOpt
    }
// ---------------------------------------------------------------------------
// Handler factory — per-session serial queue
// ---------------------------------------------------------------------------

let createHandler
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeState)
    (configLookup: ConfigLookup)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (pendingReview: (string -> bool) option)
    : (obj -> JS.Promise<FallbackHookResult>) =

    let mutable queues = Map.ofList<string, SerialQueue> []

    fun (rawEvent: obj) ->
        promise {
            let sessionID = translator.ExtractSessionID rawEvent

            let queue =
                match Map.tryFind sessionID queues with
                | Some q -> q
                | None ->
                    let q = SerialQueue()
                    queues <- Map.add sessionID q queues
                    q

            let! (result, intentOpt) =
                queue.Enqueue(fun () ->
                    handleEvent translator runtime configLookup executor workspaceRoot rawEvent pendingReview)

            match intentOpt with
            | Some intent -> do! executeContinuationIntent runtime executor workspaceRoot sessionID intent
            | None -> ()

            return result
        }
