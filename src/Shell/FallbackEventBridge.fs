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
    abstract ExtractNewUserMessageId: rawEvent: obj -> string option
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
        continuationID: string *
        continuationOrdinal: int
    | RecoverWithPromptIntent of
        model: FallbackModel *
        promptText: string *
        agent: string *
        turnId: string *
        gen: int *
        cancelGen: int *
        continuationID: string *
        continuationOrdinal: int
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

    let pending = runtime.TryGetPendingLease sessionID

    let matches =
        lease.SessionGeneration = currentGen
        && lease.HumanTurnID = currentTurnId
        && lease.CancelGeneration = currentCancelGen
        && currentOwner = "Fallback"
        && not (runtime.IsForceStopped sessionID)
        && (match stateOpt with
            | Some s -> s.Lifecycle = FallbackLifecycle.Active
            | None -> false)
        && (match pending with
            | Some p -> p.ContinuationID = lease.ContinuationID && p.Status = expectedStatus
            | None -> false)

    matches

let verifyLease (runtime: FallbackRuntimeState) (sessionID: string) (lease: PendingLease) : bool =
    verifyLeaseWithStatus "requested" runtime sessionID lease

let finishContinuation
    (runtime: FallbackRuntimeState)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (outcome: string) // "failed", "cancelled", "settled"
    (errorOrReason: string)
    : JS.Promise<unit> =
    promise {
        let isLeaseStillActive =
            match runtime.TryGetPendingLease sessionID with
            | Some pending when pending.ContinuationID = lease.ContinuationID -> true
            | _ -> false

        if isLeaseStillActive then
            if outcome = "failed" then
                do!
                    appendContinuationFailedOrFail
                        workspaceRoot
                        sessionID
                        lease.ContinuationID
                        errorOrReason
                        lease.ContinuationOrdinal
            elif outcome = "cancelled" then
                do!
                    appendContinuationCancelledOrFail
                        workspaceRoot
                        sessionID
                        lease.ContinuationID
                        errorOrReason
                        lease.ContinuationOrdinal
            elif outcome = "settled" then
                do!
                    appendContinuationSettledOrFail
                        workspaceRoot
                        sessionID
                        lease.ContinuationID
                        lease.HumanTurnID
                        lease.SessionGeneration
                        errorOrReason
                        lease.ContinuationOrdinal

        let cleared = runtime.TryClearPendingLease(sessionID, lease.ContinuationID)

        if cleared then
            if runtime.GetSessionOwner sessionID = "Fallback" then
                runtime.SetSessionOwner sessionID "None"

            runtime.SetAwaitingBusy sessionID false
            runtime.UpdateState sessionID (runtime.GetOrCreateState sessionID)
    }

let ensureActiveAndOwner (runtime: FallbackRuntimeState) (sessionID: string) (lease: PendingLease) : bool =
    let state = runtime.GetOrCreateState sessionID
    let isOwner = runtime.GetSessionOwner sessionID = "Fallback"
    let isLifecycleActive = state.Lifecycle = FallbackLifecycle.Active
    let isNotForceStopped = not (runtime.IsForceStopped sessionID)
    let isTurnValid = runtime.GetHumanTurnId sessionID = lease.HumanTurnID
    let isGenValid = runtime.GetSessionGeneration sessionID = lease.SessionGeneration

    let isCancelGenValid =
        runtime.GetCancelGeneration sessionID = lease.CancelGeneration

    isOwner
    && isLifecycleActive
    && isNotForceStopped
    && isTurnValid
    && isGenValid
    && isCancelGenValid

let executeContinuationIntent
    (runtime: FallbackRuntimeState)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (intent: ContinuationIntent)
    : JS.Promise<unit> =
    promise {
        match intent with
        | SendContinueIntent(model, agent, turnId, gen, cancelGen, continuationID, continuationOrdinal) ->
            let lease =
                { ContinuationID = continuationID
                  ContinuationOrdinal = continuationOrdinal
                  SessionGeneration = gen
                  HumanTurnID = turnId
                  CancelGeneration = cancelGen
                  Owner = "Fallback"
                  Model = model
                  PromptText = None
                  Status = "requested" }

            if
                verifyLease runtime sessionID lease
                && ensureActiveAndOwner runtime sessionID lease
            then
                try
                    do!
                        appendContinuationDispatchStartedOrFail
                            workspaceRoot
                            sessionID
                            continuationID
                            continuationOrdinal

                    // FINAL DISPATCH GATE: Atomically verify lease just before IO
                    let isLeaseStillValid =
                        runtime.TryTransitionPendingLease(
                            sessionID,
                            lease.ContinuationID,
                            "requested",
                            "dispatch_started"
                        )

                    if not isLeaseStillValid then
                        do! executor.AbortRun sessionID

                        do!
                            finishContinuation
                                runtime
                                workspaceRoot
                                sessionID
                                lease
                                "cancelled"
                                "Lease invalid at dispatch"
                    else
                        do! executor.SendContinue(sessionID, model, continuationID)

                        let isValid = verifyLeaseWithStatus "dispatch_started" runtime sessionID lease

                        if not isValid then
                            do! executor.AbortRun sessionID

                            do!
                                finishContinuation
                                    runtime
                                    workspaceRoot
                                    sessionID
                                    lease
                                    "cancelled"
                                    "Cancelled after dispatch"
                        else

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
                                    continuationOrdinal

                            if
                                not (
                                    runtime.TryTransitionPendingLease(
                                        sessionID,
                                        lease.ContinuationID,
                                        "dispatch_started",
                                        "dispatched"
                                    )
                                )
                            then
                                do! executor.AbortRun sessionID

                                do!
                                    finishContinuation
                                        runtime
                                        workspaceRoot
                                        sessionID
                                        lease
                                        "cancelled"
                                        "Cancelled after dispatch"
                            else
                                runtime.SetInjectedAt sessionID atMs
                                runtime.SetInjectedModel sessionID model
                with ex ->
                    do! finishContinuation runtime workspaceRoot sessionID lease "failed" ex.Message
            else
                do! finishContinuation runtime workspaceRoot sessionID lease "cancelled" "Lease validation failed"

        | RecoverWithPromptIntent(model, promptText, agent, turnId, gen, cancelGen, continuationID, continuationOrdinal) ->
            let lease =
                { ContinuationID = continuationID
                  ContinuationOrdinal = continuationOrdinal
                  SessionGeneration = gen
                  HumanTurnID = turnId
                  CancelGeneration = cancelGen
                  Owner = "Fallback"
                  Model = model
                  PromptText = Some promptText
                  Status = "requested" }

            if
                verifyLease runtime sessionID lease
                && ensureActiveAndOwner runtime sessionID lease
            then
                try
                    do!
                        appendContinuationDispatchStartedOrFail
                            workspaceRoot
                            sessionID
                            continuationID
                            continuationOrdinal

                    // FINAL DISPATCH GATE: Atomically verify lease just before IO
                    let isLeaseStillValid =
                        runtime.TryTransitionPendingLease(
                            sessionID,
                            lease.ContinuationID,
                            "requested",
                            "dispatch_started"
                        )

                    if not isLeaseStillValid then
                        do! executor.AbortRun sessionID

                        do!
                            finishContinuation
                                runtime
                                workspaceRoot
                                sessionID
                                lease
                                "cancelled"
                                "Lease invalid at dispatch"
                    else
                        do! executor.RecoverWithPrompt(sessionID, model, promptText, continuationID)

                        let isValid = verifyLeaseWithStatus "dispatch_started" runtime sessionID lease

                        if not isValid then
                            do! executor.AbortRun sessionID

                            do!
                                finishContinuation
                                    runtime
                                    workspaceRoot
                                    sessionID
                                    lease
                                    "cancelled"
                                    "Cancelled after dispatch"
                        else

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
                                    continuationOrdinal

                            if
                                not (
                                    runtime.TryTransitionPendingLease(
                                        sessionID,
                                        lease.ContinuationID,
                                        "dispatch_started",
                                        "dispatched"
                                    )
                                )
                            then
                                do! executor.AbortRun sessionID

                                do!
                                    finishContinuation
                                        runtime
                                        workspaceRoot
                                        sessionID
                                        lease
                                        "cancelled"
                                        "Cancelled after dispatch"
                            else
                                runtime.SetInjectedAt sessionID atMs
                                runtime.SetInjectedModel sessionID model
                with ex ->
                    do! finishContinuation runtime workspaceRoot sessionID lease "failed" ex.Message
            else
                do! finishContinuation runtime workspaceRoot sessionID lease "cancelled" "Lease validation failed"

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

        if sessionID <> "" then
            let eventType = Dyn.str rawEvent "type"
            let props = Dyn.get rawEvent "properties"

            let isAssistantMsg =
                (eventType = "message.updated" || eventType.StartsWith("message.part."))
                && not (Dyn.isNullish props)
                && (let info = Dyn.get props "info"
                    not (Dyn.isNullish info) && Dyn.str info "role" = "assistant")

            if translator.IsSessionBusy rawEvent || isAssistantMsg then
                runtime.SetBusyObserved sessionID true

        let isAwaiting = runtime.IsAwaitingBusy sessionID
        let isBusyObserved = runtime.IsBusyObserved sessionID

        let isLateIdle =
            isAwaiting
            && not isBusyObserved
            && (translator.IsSessionIdle rawEvent || translator.IsSessionError rawEvent)

        let! earlyReturnOpt =
            promise {
                if isLateIdle then
                    return
                        Some(
                            { Consumed = false
                              State = runtime.GetOrCreateState sessionID },
                            None
                        )
                else
                    return None
            }

        match earlyReturnOpt with
        | Some ret -> return ret
        | None ->

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
                    let continuationId =
                        let props = Dyn.get rawEvent "properties"
                        let props = if Dyn.isNullish props then rawEvent else props
                        let cid = Dyn.str props "continuationId"
                        let cid = if cid <> "" then cid else Dyn.str props "continuationID"
                        let cid = if cid <> "" then cid else Dyn.str rawEvent "continuationId"
                        let cid = if cid <> "" then cid else Dyn.str rawEvent "continuationID"
                        cid

                    let isContinuationIdMismatch =
                        match runtime.TryGetPendingLease sessionID with
                        | Some lease -> continuationId <> "" && continuationId <> lease.ContinuationID
                        | None -> false

                    if evt = FallbackEvent.NewUserMessage then
                        isContinuationIdMismatch
                    else if isContinuationIdMismatch then
                        true
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
                        let settleInfo = runtime.TryGetSettleInfo(sessionID, activeComp)

                        match settleInfo with
                        | Some(_, ordinal) ->
                            do! appendCompactionSettledOrFail workspaceRoot sessionID activeComp "cancelled" ordinal
                            let _ = runtime.ApplySettle(sessionID, activeComp)
                            ()
                        | None -> ()

                    let msgId = translator.ExtractNewUserMessageId rawEvent |> Option.defaultValue ""
                    let lastMsgId = runtime.GetLastHumanMessageId sessionID

                    if msgId = "" || msgId <> lastMsgId then
                        match runtime.TryGetPendingNudgeLease sessionID with
                        | Some nudgeLease ->
                            do!
                                appendNudgeCancelledOrFail
                                    workspaceRoot
                                    sessionID
                                    nudgeLease.NudgeID
                                    "New user message"
                                    nudgeLease.NudgeOrdinal

                            let _ = runtime.ApplyCancelNudgeLease(sessionID, nudgeLease.NudgeID)
                            ()
                        | None -> ()

                        runtime.SetChain sessionID []
                        runtime.ClearModel sessionID
                        runtime.ClearInjected sessionID
                        runtime.SetSessionOwner sessionID "Human"
                        let turnId = runtime.IncrementHumanTurnId sessionID
                        runtime.SetLastHumanMessageId sessionID msgId
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
                        let humanTurnOrdinal = runtime.GetHumanTurnOrdinal sessionID

                        do!
                            appendHumanTurnStartedOrFail
                                workspaceRoot
                                sessionID
                                turnId
                                provider
                                model
                                variant
                                agent
                                humanTurnOrdinal
                                msgId

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
                                do! finishContinuation runtime workspaceRoot sessionID lease "cancelled" "User aborted"
                            | None -> ()

                            match runtime.TryGetPendingNudgeLease sessionID with
                            | Some nudgeLease ->
                                do!
                                    appendNudgeCancelledOrFail
                                        workspaceRoot
                                        sessionID
                                        nudgeLease.NudgeID
                                        "User aborted"
                                        nudgeLease.NudgeOrdinal

                                let _ = runtime.ApplyCancelNudgeLease(sessionID, nudgeLease.NudgeID)
                                ()
                            | None -> ()

                        let mutable finalState = ns

                        if evt = FallbackEvent.SessionBusy then
                            match runtime.TryGetPendingLease sessionID with
                            | Some lease when lease.Status = "dispatch_started" || lease.Status = "dispatched" ->
                                let runningLease = { lease with Status = "running" }
                                runtime.SetPendingLease(sessionID, runningLease)
                            | _ -> ()

                            match runtime.TryGetPendingNudgeLease sessionID with
                            | Some lease when lease.Status = "dispatch_started" || lease.Status = "dispatched" ->
                                let runningLease = { lease with Status = "running" }
                                runtime.SetPendingNudgeLease(sessionID, runningLease)
                            | _ -> ()

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
                                    let continuationOrdinal = runtime.IncrementContinuationOrdinal sessionID

                                    let lease =
                                        { ContinuationID = continuationID
                                          ContinuationOrdinal = continuationOrdinal
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
                                            continuationOrdinal

                                    let intent =
                                        SendContinueIntent(
                                            model,
                                            agent,
                                            runtime.GetHumanTurnId sessionID,
                                            currentGen,
                                            currentCancelGen,
                                            continuationID,
                                            continuationOrdinal
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
                                    let continuationOrdinal = runtime.IncrementContinuationOrdinal sessionID

                                    let lease =
                                        { ContinuationID = continuationID
                                          ContinuationOrdinal = continuationOrdinal
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
                                            continuationOrdinal

                                    let intent =
                                        RecoverWithPromptIntent(
                                            model,
                                            promptText,
                                            agent,
                                            runtime.GetHumanTurnId sessionID,
                                            currentGen,
                                            currentCancelGen,
                                            continuationID,
                                            continuationOrdinal
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
                                                let continuationOrdinal = runtime.IncrementContinuationOrdinal sessionID

                                                let lease =
                                                    { ContinuationID = continuationID
                                                      ContinuationOrdinal = continuationOrdinal
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
                                                        continuationOrdinal

                                                let intent =
                                                    RecoverWithPromptIntent(
                                                        model,
                                                        promptText,
                                                        agent,
                                                        runtime.GetHumanTurnId sessionID,
                                                        currentGen,
                                                        currentCancelGen,
                                                        continuationID,
                                                        continuationOrdinal
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

                        let isPostTerminal =
                            evt <> FallbackEvent.SessionBusy
                            && (finalState2.Lifecycle = FallbackLifecycle.TaskComplete
                                || finalState2.Lifecycle = FallbackLifecycle.Cancelled
                                || finalState2.Phase = FallbackPhase.Exhausted
                                || (finalState2.Phase = FallbackPhase.Idle && intentOpt.IsNone))

                        if isPostTerminal then
                            match runtime.TryGetPendingLease sessionID with
                            | Some lease ->
                                if lease.Status <> "cancelled" then
                                    do!
                                        appendContinuationSettledOrFail
                                            workspaceRoot
                                            sessionID
                                            lease.ContinuationID
                                            lease.HumanTurnID
                                            lease.SessionGeneration
                                            "completed"
                                            lease.ContinuationOrdinal

                                if runtime.TryClearPendingLease(sessionID, lease.ContinuationID) then
                                    if runtime.GetSessionOwner sessionID = "Fallback" then
                                        runtime.SetSessionOwner sessionID "None"

                                    runtime.SetAwaitingBusy sessionID false
                            | None -> ()

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
