module Wanxiangshu.Runtime.Fallback.LeaseValidationRules

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.EpisodeIdentity


let verifyLeaseWithStatus
    (expectedStatus: LeaseStatus)
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (lease: PendingLease)
    : bool =
    let stateOpt = runtime.TryGetState sessionID
    let pending = (runtime.GetSession sessionID).PendingLease

    let s = runtime.GetSession sessionID

    continuationLeaseIsCurrent lease s
    && not s.CompactionForceStopped
    && s.CompactionActiveId = ""
    && not s.CompactionCompacted
    && (match stateOpt with
        | Some st -> st.Lifecycle = FallbackLifecycle.Active
        | None -> false)
    && (match pending with
        | Some p -> p.ContinuationID = lease.ContinuationID && p.Status = expectedStatus
        | None -> false)

let verifyLease (runtime: FallbackRuntimeStore) (sessionID: string) (lease: PendingLease) : bool =
    verifyLeaseWithStatus LeaseStatus.Requested runtime sessionID lease

let ensureActiveAndOwner (runtime: FallbackRuntimeStore) (sessionID: string) (lease: PendingLease) : bool =
    let state = runtime.GetOrCreateState sessionID

    let s = runtime.GetSession sessionID

    state.Lifecycle = FallbackLifecycle.Active
    && continuationLeaseIsCurrent lease s
    && not s.CompactionForceStopped
    && s.CompactionActiveId = ""
    && not s.CompactionCompacted

let checkIsStale
    (isEventContIdMatch: bool)
    (eventOpt: FallbackEvent option)
    (eventTurnIdOpt: string option)
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    : bool =
    match eventOpt with
    | None -> false
    | Some evt ->
        let s = runtime.GetSession sessionID
        let isNewUser = evt = FallbackEvent.NewUserMessage

        let isAbortError =
            match evt with
            | FallbackEvent.SessionError err -> Wanxiangshu.Kernel.FallbackKernel.Decision.errorInputIsAbort err
            | _ -> false

        let lifecycleCancelled =
            match runtime.TryGetState sessionID with
            | Some st -> st.Lifecycle = FallbackLifecycle.Cancelled
            | None -> false

        not (
            isAccepted (
                disposeContinuationSessionEvent
                    isEventContIdMatch
                    eventTurnIdOpt
                    isNewUser
                    isAbortError
                    lifecycleCancelled
                    s
            )
        )

/// Strict attribution order (SPEC F-02 / §七 step 5):
/// 1. assistant parentID == HostUserMessageId
/// 2. host run id == HostRunId (when recorded; currently only HostUserMessageId path)
/// 3. verified namespaced dispatch marker on host message
/// 4. else Unmatched — never generation-only
type ContinuationMatchClassification =
    | MatchedByParentId of parentId: string
    | MatchedByHostRunId of runId: string
    | MatchedByMarker of cid: string
    | UnmatchedStatusHint of continuationIdMissing: bool

let private isHostAccepted (lease: PendingLease) : bool =
    lease.HostUserMessageId <> ""
    && match lease.Status with
       | LeaseStatus.Dispatched
       | LeaseStatus.AcceptanceUnknown
       | LeaseStatus.Running -> true
       | LeaseStatus.Requested
       | LeaseStatus.DispatchStarted
       | LeaseStatus.Cancelled
       | LeaseStatus.Settled -> false

let classifyContinuationMatch
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (continuationId: string)
    (parentIDOpt: string option)
    (hostRunIDOpt: string option)
    : ContinuationMatchClassification =
    match (runtime.GetSession sessionID).PendingLease with
    | None ->
        // No pending lease: an event with a continuation id is a stale
        // continuation-tagged event and must not be treated as a current
        // status hint. An empty continuation id is a normal status hint.
        UnmatchedStatusHint(continuationId = "")
    | Some lease ->
        // Strict correlation order (SPEC F-02):
        // 1. assistant.parentID == HostUserMessageId
        // 2. HostRunId equal
        // 3. verified namespaced dispatch marker on real host message
        // 4. else Unmatched
        // HumanTurnID and SessionGeneration alone never establish ownership.
        let isParentIdMatch =
            match parentIDOpt with
            | Some pid when pid <> "" && lease.HostUserMessageId <> "" && pid = lease.HostUserMessageId -> true
            | _ -> false

        if isParentIdMatch then
            MatchedByParentId parentIDOpt.Value
        else
            // Host run id is only strong evidence when the lease already holds
            // a HostUserMessageId and the run id equals that same identity
            // (OpenCode often reuses the user message id as the turn/run key).
            let isHostRunIdMatch =
                match hostRunIDOpt with
                | Some rid when rid <> "" ->
                    (lease.HostRunId <> "" && rid = lease.HostRunId)
                    || (lease.HostUserMessageId <> "" && rid = lease.HostUserMessageId)
                | _ -> false

            if isHostRunIdMatch then
                MatchedByHostRunId hostRunIDOpt.Value
            elif continuationId <> "" && continuationId = lease.ContinuationID then
                MatchedByMarker continuationId
            else
                UnmatchedStatusHint(continuationId = "")

let checkContinuationMatchesWithEvidence
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (continuationId: string)
    (parentIDOpt: string option)
    (hostRunIDOpt: string option)
    : bool * bool =
    let classification =
        classifyContinuationMatch runtime sessionID continuationId parentIDOpt hostRunIDOpt

    let isMatched =
        match classification with
        | MatchedByParentId _
        | MatchedByHostRunId _
        | MatchedByMarker _ -> true
        | _ -> false

    let isContIdMatch =
        match classification with
        | MatchedByParentId _
        | MatchedByHostRunId _
        | MatchedByMarker _ -> true
        | UnmatchedStatusHint true -> true
        | UnmatchedStatusHint false -> false

    isMatched, isContIdMatch

let checkContinuationMatches
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (continuationId: string)
    : bool * bool =
    checkContinuationMatchesWithEvidence runtime sessionID continuationId None None

let isTerminalOrSettled
    (evt: FallbackEvent)
    (currentState: SessionFallbackState)
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    : bool =
    let terminalSessionFallbackState =
        currentState.Lifecycle = FallbackLifecycle.Cancelled
        || currentState.Lifecycle = FallbackLifecycle.TaskComplete

    let settledFallbackLease =
        match (runtime.GetSession sessionID).PendingLease with
        | Some lease -> lease.Status = LeaseStatus.Settled || lease.Status = LeaseStatus.Cancelled
        | None -> false

    evt <> FallbackEvent.NewUserMessage
    && (terminalSessionFallbackState || settledFallbackLease)

/// Idle disposition for an identified physical session (SPEC §七 step 6).
type IdleDisposition =
    /// No active dispatch: record a session hint only; never cache for next turn.
    | SessionHintOnly
    /// Active dispatch has not reached HostAccepted — idle cannot terminalise it.
    | RejectNotHostAccepted of continuationId: string
    /// HostAccepted but no strong assistant/error terminal yet → reconciliation.
    | NeedsReconciliation of continuationId: string * hostUserMessageId: string
    /// HostAccepted and strong terminal evidence present → may settle.
    | MaySettle of continuationId: string * hostUserMessageId: string
    /// Duplicate idle while already terminal / already settled — ignore.
    | IdempotentIgnore

let classifyIdleDisposition
    (session: FallbackSessionRuntime)
    (hasStrongTerminalEvidence: bool)
    (isDuplicateIdle: bool)
    : IdleDisposition =
    if isDuplicateIdle then
        IdempotentIgnore
    else
        match session.PendingLease with
        | None ->
            match session.PendingNudgeLease with
            | None -> SessionHintOnly
            | Some nl ->
                if nl.HostUserMessageId = "" then
                    RejectNotHostAccepted nl.NudgeID
                elif hasStrongTerminalEvidence then
                    MaySettle(nl.NudgeID, nl.HostUserMessageId)
                else
                    NeedsReconciliation(nl.NudgeID, nl.HostUserMessageId)
        | Some lease ->
            match lease.Status with
            | LeaseStatus.Settled
            | LeaseStatus.Cancelled -> IdempotentIgnore
            | LeaseStatus.Requested
            | LeaseStatus.DispatchStarted when lease.HostUserMessageId = "" ->
                RejectNotHostAccepted lease.ContinuationID
            | _ when not (isHostAccepted lease) && lease.HostUserMessageId = "" ->
                RejectNotHostAccepted lease.ContinuationID
            | _ when hasStrongTerminalEvidence && lease.HostUserMessageId <> "" ->
                MaySettle(lease.ContinuationID, lease.HostUserMessageId)
            | _ when lease.HostUserMessageId <> "" -> NeedsReconciliation(lease.ContinuationID, lease.HostUserMessageId)
            | _ -> RejectNotHostAccepted lease.ContinuationID

/// True when idle may drive a domain terminal settlement for the pending lease.
let idleMaySettle (session: FallbackSessionRuntime) (hasStrongTerminalEvidence: bool) (isDuplicateIdle: bool) : bool =
    match classifyIdleDisposition session hasStrongTerminalEvidence isDuplicateIdle with
    | MaySettle _ -> true
    | _ -> false
