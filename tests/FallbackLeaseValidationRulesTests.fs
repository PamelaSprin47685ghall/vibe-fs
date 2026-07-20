module Wanxiangshu.Tests.FallbackLeaseValidationRulesTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeaseAcceptancePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.LeaseValidationRules

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
          HostRunId = ""
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

let private bindHostIds hostUserMessageId hostRunId (rt: FallbackRuntimeStore) =
    rt.UpdateSession(
        sid,
        fun s ->
            match s.PendingLease with
            | Some l ->
                { s with
                    PendingLease =
                        Some
                            { l with
                                HostUserMessageId = hostUserMessageId
                                HostRunId = hostRunId } }
            | None -> s
    )

let checkContinuationMatches_matchByParentId () =
    let rt, _ = setupDispatched ()
    bindHostIds "host-user-msg-1" "" rt
    let isMatched, isContIdMatch =
        checkContinuationMatchesWithEvidence rt sid "" (Some "host-user-msg-1") None

    chk "match by HostUserMessageId parent isMatched true" isMatched
    chk "match by HostUserMessageId parent isContIdMatch true" isContIdMatch

let checkContinuationMatches_matchByHostRunId () =
    let rt, _ = setupDispatched ()
    bindHostIds "" "host-run-1" rt
    let isMatched, isContIdMatch =
        checkContinuationMatchesWithEvidence rt sid "" None (Some "host-run-1")

    chk "match by HostRunId isMatched true" isMatched
    chk "match by HostRunId isContIdMatch true" isContIdMatch

let checkContinuationMatches_humanTurnIdEqualsParentButHostUserMessageIdEmpty_unmatched () =
    let rt, lease = setupDispatched ()
    chk "HostUserMessageId starts empty" (lease.HostUserMessageId = "")
    let isMatched, isContIdMatch =
        checkContinuationMatchesWithEvidence rt sid "" (Some lease.HumanTurnID) None

    chk "HumanTurnID==parent but HostUserMessageId empty isMatched false" (not isMatched)
    chk "HumanTurnID==parent but HostUserMessageId empty isContIdMatch true" isContIdMatch

let checkContinuationMatches_wrongHostUserMessageId_unmatched () =
    let rt, _ = setupDispatched ()
    bindHostIds "host-user-msg-1" "" rt
    let isMatched, isContIdMatch =
        checkContinuationMatchesWithEvidence rt sid "" (Some "wrong-host-user-msg") None

    chk "wrong HostUserMessageId isMatched false" (not isMatched)
    chk "wrong HostUserMessageId isContIdMatch true" isContIdMatch

let checkContinuationMatches_generationEqualAlone_unmatched () =
    let rt, lease = setupDispatched ()
    // Same session generation is already true via setup; no parent/run/marker → Unmatched.
    chk "generation equal on lease" (lease.SessionGeneration = (rt.GetSession sid).SessionGeneration)
    let isMatched, isContIdMatch = checkContinuationMatchesWithEvidence rt sid "" None None
    chk "generation-equal alone isMatched false" (not isMatched)
    chk "generation-equal alone isContIdMatch true" isContIdMatch

let checkContinuationMatches_unmatchedStatusHint () =
    let rt, _ = setupDispatched ()
    bindHostIds "host-user-msg-1" "host-run-1" rt
    let isMatched, isContIdMatch =
        checkContinuationMatchesWithEvidence rt sid "" (Some "diff-parent") (Some "diff-run")

    chk "unmatched status hint isMatched false" (not isMatched)
    chk "unmatched status hint isContIdMatch true" isContIdMatch

let checkContinuationMatches_mismatchedContinuationId () =
    let rt, _ = setupDispatched ()
    let isMatched, isContIdMatch = checkContinuationMatchesWithEvidence rt sid "different-cid" None None
    chk "mismatched continuation id isMatched false" (not isMatched)
    chk "mismatched continuation id isContIdMatch false" (not isContIdMatch)

let tryAcceptPendingLease_bindsHostUserMessageId () =
    let rt, lease = setupDispatched ()
    chk "accept starts with empty HostUserMessageId" (lease.HostUserMessageId = "")
    let accepted =
        rt.UpdateSessionReturning(sid, tryAcceptPendingLeaseReturning lease.ContinuationID "msg-bound-1")

    chk "accept returns true" accepted
    let after = (rt.GetSession sid).PendingLease |> Option.get
    chk "HostUserMessageId bound on accept" (after.HostUserMessageId = "msg-bound-1")
    chk "status Dispatched after accept" (after.Status = LeaseStatus.Dispatched)

    let isMatched, _ =
        checkContinuationMatchesWithEvidence rt sid "" (Some "msg-bound-1") None

    chk "parentID matches bound HostUserMessageId" isMatched

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
    checkContinuationMatches_humanTurnIdEqualsParentButHostUserMessageIdEmpty_unmatched ()
    checkContinuationMatches_wrongHostUserMessageId_unmatched ()
    checkContinuationMatches_generationEqualAlone_unmatched ()
    checkContinuationMatches_unmatchedStatusHint ()
    checkContinuationMatches_mismatchedContinuationId ()
    tryAcceptPendingLease_bindsHostUserMessageId ()
    checkIsStale_noneEvent_returnsFalse ()
    checkIsStale_newUserMessage_returnsFalse ()
    checkIsStale_contIdNotMatch_returnsTrue ()
    isTerminalOrSettled_newUserMessage_returnsFalse ()
    isTerminalOrSettled_cancelledLifecycle_returnsTrue ()
    isTerminalOrSettled_settledLease_returnsTrue ()
