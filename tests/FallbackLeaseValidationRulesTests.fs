module Wanxiangshu.Tests.FallbackLeaseValidationRulesTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeCompactionPure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.LeaseValidationRules
// IdleDisposition cases live here (SessionHintOnly / MaySettle / …)

let private sid = "test-session"

let private mkModel () : FallbackModel =
    { ProviderID = "p"
      ModelID = "m"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private setupDispatched () =
    let rt = FallbackRuntimeStore()

    rt.Update(sid, beginHumanTurn "msg-1")
    rt.Update(sid, startDispatch (mkModel ()) None)

    let lease = (rt.GetSession sid).PendingLease |> Option.get
    rt, lease

// ---------------------------------------------------------------------------
// verifyLeaseWithStatus
// ---------------------------------------------------------------------------

let verifyLeaseWithStatus_allMatch_returnsTrue () =
    let rt, lease = setupDispatched ()
    chk "all match returns true" (verifyLeaseWithStatus LeaseStatus.Requested rt sid lease)

let verifyLeaseWithStatus_generationMismatch_returnsFalse () =
    let rt, lease = setupDispatched ()
    rt.Update(sid, setSessionGeneration 99)
    chk "gen mismatch returns false" (not (verifyLeaseWithStatus LeaseStatus.Requested rt sid lease))

let verifyLeaseWithStatus_ownerNotFallback_returnsFalse () =
    let rt, lease = setupDispatched ()
    rt.Update(sid, transferOwnership SessionOwner.NoOwner)
    chk "owner not fallback returns false" (not (verifyLeaseWithStatus LeaseStatus.Requested rt sid lease))

let verifyLeaseWithStatus_lifecycleCancelled_returnsFalse () =
    let rt, lease = setupDispatched ()

    let cancelledState =
        { (rt.GetOrCreateState sid) with
            Lifecycle = FallbackLifecycle.Cancelled }

    rt.Update(sid, setCore cancelledState)
    chk "lifecycle cancelled returns false" (not (verifyLeaseWithStatus LeaseStatus.Requested rt sid lease))

let verifyLeaseWithStatus_compactionForceStopped_returnsFalse () =
    let rt, lease = setupDispatched ()
    rt.Update(sid, markForceStopped)
    chk "force stopped returns false" (not (verifyLeaseWithStatus LeaseStatus.Requested rt sid lease))

let verifyLeaseWithStatus_noPendingLease_returnsFalse () =
    let rt = FallbackRuntimeStore()
    rt.Update(sid, beginHumanTurn "msg-1")

    let fakeLease =
        { ContinuationID = "x"
          ContinuationOrdinal = 0
          SessionGeneration = 0
          HumanTurnID = ""
          HostUserMessageId = ""
          CancelGeneration = 0
          Owner = SessionOwner.Fallback
          Model = mkModel ()
          PromptText = None
          Status = LeaseStatus.Requested }

    chk "no pending returns false" (not (verifyLeaseWithStatus LeaseStatus.Requested rt sid fakeLease))

let verifyLeaseWithStatus_statusMismatch_returnsFalse () =
    let rt, lease = setupDispatched ()

    chk "status mismatch returns false" (not (verifyLeaseWithStatus LeaseStatus.DispatchStarted rt sid lease))

// ---------------------------------------------------------------------------
// checkContinuationMatches
// ---------------------------------------------------------------------------

let checkContinuationMatches_emptyContinuationId () =
    let rt, _ = setupDispatched ()
    let isMatched, isContIdMatch = checkContinuationMatches rt sid ""
    chk "empty contId isMatched false" (not isMatched)
    chk "empty contId isContIdMatch true" isContIdMatch

let checkContinuationMatches_matchingId () =
    let rt, lease = setupDispatched ()
    let isMatched, isContIdMatch = checkContinuationMatches rt sid lease.ContinuationID
    chk "matching contId isMatched true" isMatched
    chk "matching contId isContIdMatch true" isContIdMatch

let checkContinuationMatches_noPending () =
    let rt = FallbackRuntimeStore()
    let isMatched, isContIdMatch = checkContinuationMatches rt sid "x"
    chk "no pending isMatched false" (not isMatched)
    chk "no pending with contId is stale (contIdMatch false)" (not isContIdMatch)

let checkContinuationMatches_noPendingEmptyContId () =
    let rt = FallbackRuntimeStore()
    let isMatched, isContIdMatch = checkContinuationMatches rt sid ""
    chk "no pending empty contId isMatched false" (not isMatched)
    chk "no pending empty contId isContIdMatch true" isContIdMatch

let checkContinuationMatches_matchByParentId () =
    let rt, lease = setupDispatched ()
    // F-02: parentID matches HostUserMessageId only — not HumanTurnID.
    let hostMsg = "host-user-msg-1"

    rt.UpdateSession(
        sid,
        fun s ->
            match s.PendingLease with
            | Some l ->
                { s with
                    PendingLease =
                        Some
                            { l with
                                HostUserMessageId = hostMsg
                                Status = LeaseStatus.Dispatched } }
            | None -> s
    )

    let isMatched, isContIdMatch = checkContinuationMatchesWithEvidence rt sid "" (Some hostMsg) None
    chk "match by parent id isMatched true" isMatched
    chk "match by parent id isContIdMatch true" isContIdMatch

    let noMatchOnHumanTurn, _ =
        checkContinuationMatchesWithEvidence rt sid "" (Some lease.HumanTurnID) None

    chk "humanTurnId alone is NOT parent match" (not noMatchOnHumanTurn)

let checkContinuationMatches_matchByHostRunId () =
    let rt, _ = setupDispatched ()
    let hostMsg = "host-run-as-msg"

    rt.UpdateSession(
        sid,
        fun s ->
            match s.PendingLease with
            | Some l ->
                { s with
                    PendingLease =
                        Some
                            { l with
                                HostUserMessageId = hostMsg
                                Status = LeaseStatus.Dispatched } }
            | None -> s
    )

    let isMatched, isContIdMatch = checkContinuationMatchesWithEvidence rt sid "" None (Some hostMsg)
    chk "match by host run id isMatched true" isMatched
    chk "match by host run id isContIdMatch true" isContIdMatch

let checkContinuationMatches_unmatchedStatusHint () =
    let rt, _ = setupDispatched ()
    let isMatched, isContIdMatch = checkContinuationMatchesWithEvidence rt sid "" (Some "diff-parent") (Some "diff-run")
    chk "unmatched status hint isMatched false" (not isMatched)
    chk "unmatched status hint isContIdMatch true" isContIdMatch

let checkContinuationMatches_parentIdWithoutHostBinding_unmatched () =
    let rt, _ = setupDispatched ()
    // Pending lease has empty HostUserMessageId — parentID cannot prove ownership.
    let isMatched, isContIdMatch =
        checkContinuationMatchesWithEvidence rt sid "" (Some "orphan-parent") None

    chk "unbound parent id isMatched false" (not isMatched)
    chk "unbound parent id isContIdMatch true (status hint)" isContIdMatch

// ---------------------------------------------------------------------------
// classifyIdleDisposition (SPEC §七 step 6)
// ---------------------------------------------------------------------------

let classifyIdle_noActive_sessionHintOnly () =
    let rt = FallbackRuntimeStore()
    let d = classifyIdleDisposition (rt.GetSession sid) false false
    chk "no active → SessionHintOnly" (d = SessionHintOnly)

let classifyIdle_notHostAccepted_reject () =
    let rt, lease = setupDispatched ()
    let d = classifyIdleDisposition (rt.GetSession sid) false false

    match d with
    | RejectNotHostAccepted cid -> chk "reject not accepted" (cid = lease.ContinuationID)
    | other -> failwith ("expected RejectNotHostAccepted, got " + string other)

let classifyIdle_hostAccepted_noStrong_reconciliation () =
    let rt, lease = setupDispatched ()

    rt.UpdateSession(
        sid,
        fun s ->
            match s.PendingLease with
            | Some l ->
                { s with
                    PendingLease =
                        Some
                            { l with
                                HostUserMessageId = "hm-1"
                                Status = LeaseStatus.Dispatched } }
            | None -> s
    )

    let d = classifyIdleDisposition (rt.GetSession sid) false false

    match d with
    | NeedsReconciliation(cid, mid) ->
        chk "recon cid" (cid = lease.ContinuationID)
        chk "recon host msg" (mid = "hm-1")
    | other -> failwith ("expected NeedsReconciliation, got " + string other)

let classifyIdle_hostAccepted_strong_maySettle () =
    let rt, lease = setupDispatched ()

    rt.UpdateSession(
        sid,
        fun s ->
            match s.PendingLease with
            | Some l ->
                { s with
                    PendingLease =
                        Some
                            { l with
                                HostUserMessageId = "hm-2"
                                Status = LeaseStatus.Running } }
            | None -> s
    )

    let d = classifyIdleDisposition (rt.GetSession sid) true false

    match d with
    | MaySettle(cid, mid) ->
        chk "settle cid" (cid = lease.ContinuationID)
        chk "settle host msg" (mid = "hm-2")
    | other -> failwith ("expected MaySettle, got " + string other)

let classifyIdle_duplicate_idempotent () =
    let rt, _ = setupDispatched ()
    let d = classifyIdleDisposition (rt.GetSession sid) false true
    chk "duplicate idle → IdempotentIgnore" (d = IdempotentIgnore)

let classifyIdle_settledLease_idempotent () =
    let rt, lease = setupDispatched ()

    rt.UpdateSession(
        sid,
        fun s ->
            match s.PendingLease with
            | Some l ->
                { s with
                    PendingLease = Some { l with Status = LeaseStatus.Settled } }
            | None -> s
    )

    let d = classifyIdleDisposition (rt.GetSession sid) false false
    chk "settled lease → IdempotentIgnore" (d = IdempotentIgnore)
    chk "lease id preserved for diagnostics" (lease.ContinuationID <> "")

let checkContinuationMatches_mismatchedContinuationId () =
    let rt, _ = setupDispatched ()
    let isMatched, isContIdMatch = checkContinuationMatchesWithEvidence rt sid "different-cid" None None
    chk "mismatched continuation id isMatched false" (not isMatched)
    chk "mismatched continuation id isContIdMatch false" (not isContIdMatch)

// ---------------------------------------------------------------------------
// checkIsStale
// ---------------------------------------------------------------------------

let checkIsStale_noneEvent_returnsFalse () =
    let rt = FallbackRuntimeStore()
    chk "none event not stale" (not (checkIsStale true None None rt sid))

let checkIsStale_newUserMessage_returnsFalse () =
    let rt = FallbackRuntimeStore()
    chk "new user message not stale" (not (checkIsStale true (Some FallbackEvent.NewUserMessage) None rt sid))

let checkIsStale_contIdNotMatch_returnsTrue () =
    let rt, _ = setupDispatched ()
    chk "contId not match is stale" (checkIsStale false (Some FallbackEvent.NewUserMessage) None rt sid)

// ---------------------------------------------------------------------------
// isTerminalOrSettled
// ---------------------------------------------------------------------------

let isTerminalOrSettled_newUserMessage_returnsFalse () =
    let rt = FallbackRuntimeStore()
    chk "new user message not terminal" (not (isTerminalOrSettled FallbackEvent.NewUserMessage freshState rt sid))

let isTerminalOrSettled_cancelledLifecycle_returnsTrue () =
    let rt = FallbackRuntimeStore()

    let cancelledState =
        { freshState with
            Lifecycle = FallbackLifecycle.Cancelled }

    let errEvt =
        FallbackEvent.SessionError
            { ErrorName = "e"
              DomainError = None
              Message = ""
              StatusCode = None
              IsRetryable = None }

    chk "cancelled lifecycle is terminal" (isTerminalOrSettled errEvt cancelledState rt sid)

let isTerminalOrSettled_settledLease_returnsTrue () =
    let rt, lease = setupDispatched ()

    rt.UpdateSession(
        sid,
        fun s ->
            match tryTransitionPendingLease lease.ContinuationID LeaseStatus.Requested LeaseStatus.Settled s with
            | Some s' -> s'
            | None -> s
    )

    let errEvt =
        FallbackEvent.SessionError
            { ErrorName = "e"
              DomainError = None
              Message = ""
              StatusCode = None
              IsRetryable = None }

    chk "settled lease is terminal" (isTerminalOrSettled errEvt freshState rt sid)

// ---------------------------------------------------------------------------
// Suite entry
// ---------------------------------------------------------------------------

let run () =
    verifyLeaseWithStatus_allMatch_returnsTrue ()
    verifyLeaseWithStatus_generationMismatch_returnsFalse ()
    verifyLeaseWithStatus_ownerNotFallback_returnsFalse ()
    verifyLeaseWithStatus_lifecycleCancelled_returnsFalse ()
    verifyLeaseWithStatus_compactionForceStopped_returnsFalse ()
    verifyLeaseWithStatus_noPendingLease_returnsFalse ()
    verifyLeaseWithStatus_statusMismatch_returnsFalse ()
    checkContinuationMatches_emptyContinuationId ()
    checkContinuationMatches_matchingId ()
    checkContinuationMatches_noPending ()
    checkContinuationMatches_noPendingEmptyContId ()
    checkContinuationMatches_matchByParentId ()
    checkContinuationMatches_matchByHostRunId ()
    checkContinuationMatches_unmatchedStatusHint ()
    checkContinuationMatches_mismatchedContinuationId ()
    checkContinuationMatches_parentIdWithoutHostBinding_unmatched ()
    checkIsStale_noneEvent_returnsFalse ()
    checkIsStale_newUserMessage_returnsFalse ()
    checkIsStale_contIdNotMatch_returnsTrue ()
    isTerminalOrSettled_newUserMessage_returnsFalse ()
    isTerminalOrSettled_cancelledLifecycle_returnsTrue ()
    isTerminalOrSettled_settledLease_returnsTrue ()
    classifyIdle_noActive_sessionHintOnly ()
    classifyIdle_notHostAccepted_reject ()
    classifyIdle_hostAccepted_noStrong_reconciliation ()
    classifyIdle_hostAccepted_strong_maySettle ()
    classifyIdle_duplicate_idempotent ()
    classifyIdle_settledLease_idempotent ()
