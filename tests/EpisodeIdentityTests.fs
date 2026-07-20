module Wanxiangshu.Tests.EpisodeIdentityTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.EpisodeIdentity

let private dummyModel =
    { ProviderID = "p"
      ModelID = "m"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private mkContinuationLease id ordinal epoch status : PendingLease =
    { ContinuationID = id
      ContinuationOrdinal = ordinal
      SessionGeneration = epoch.SessionGeneration
      HumanTurnID = epoch.HumanTurnId
      HostUserMessageId = ""
      HostRunId = ""
      CancelGeneration = epoch.CancelGeneration
      Owner = SessionOwner.Fallback
      Model = dummyModel
      PromptText = None
      Status = status }

let private withContinuation lease (s: FallbackSessionRuntime) =
    { s with
        Owner = SessionOwner.Fallback
        PendingLease = Some lease
        SessionGeneration = lease.SessionGeneration
        CancelGeneration = lease.CancelGeneration
        HumanTurnId = lease.HumanTurnID
        ActiveContinuationGen = lease.SessionGeneration
        ActiveContinuationCancelGen = lease.CancelGeneration
        Core = { s.Core with Lifecycle = FallbackLifecycle.Active } }

// late idle from episode A cannot settle episode B
let lateIdleFromEpisodeACannotSettleEpisodeB () =
    let epochA =
        { SessionGeneration = 1
          CancelGeneration = 1
          HumanTurnId = "ht-a" }

    let epochB =
        { SessionGeneration = 1
          CancelGeneration = 1
          HumanTurnId = "ht-a" }

    let leaseA = mkContinuationLease "cont-a" 1 epochA LeaseStatus.Dispatched
    let leaseB = mkContinuationLease "cont-b" 2 epochB LeaseStatus.Dispatched
    let s = freshSessionState |> withContinuation leaseB
    let claimedA = ofContinuationLease leaseA
    let d = disposeLateEvent claimedA s
    equal "late A discarded as id mismatch" LateEventDisposition.DiscardIdMismatch d
    check "A does not match active B" (not (matchesActive claimedA s))
    check "B is current" (continuationLeaseIsCurrent leaseB s)

// generation bump discards stale
let generationBumpDiscardsStaleContinuation () =
    let epoch =
        { SessionGeneration = 3
          CancelGeneration = 2
          HumanTurnId = "ht-1" }

    let lease = mkContinuationLease "cont-1" 1 epoch LeaseStatus.Running
    let s0 = freshSessionState |> withContinuation lease
    let claimed = ofContinuationLease lease
    equal "fresh accepted" LateEventDisposition.Accept (disposeLateEvent claimed s0)

    let sBump = s0 |> setSessionGeneration 4
    equal "gen bump discards" LateEventDisposition.DiscardStaleEpoch (disposeLateEvent claimed sBump)
    check "lease not current after gen bump" (not (continuationLeaseIsCurrent lease sBump))

    let sCancel = s0 |> setCancelGeneration 9
    equal "cancel bump discards" LateEventDisposition.DiscardStaleEpoch (disposeLateEvent claimed sCancel)

// compaction settle scoped to active id + epoch
let compactionSettleScopedToActiveEpisode () =
    let baseState = freshSessionState |> beginHumanTurn "msg-1"
    let s =
        baseState
        |> startCompaction "comp-a" 1 baseState.HumanTurnId baseState.CancelGeneration 1

    check "active a settle ok" (canSettleCompaction "comp-a" s)
    check "foreign b settle rejected" (not (canSettleCompaction "comp-b" s))
    applySettle "comp-b" s |> isNone
    applySettle "comp-a" s |> isSome

    let settled = applySettle "comp-a" s |> Option.get
    check "settle clears active" (settled.CompactionActiveId = "")
    check "late settle after clear rejected" (not (canSettleCompaction "comp-a" settled))

// cancel generation bump rejects compaction settle from old episode
let compactionSettleRejectsStaleCancelGeneration () =
    let baseState = freshSessionState |> beginHumanTurn "msg-1"
    let s0 =
        baseState
        |> startCompaction "comp-1" 1 baseState.HumanTurnId baseState.CancelGeneration 1

    let s1 = { s0 with CancelGeneration = s0.CancelGeneration + 1 }

    check "stale cancel rejects settle" (not (canSettleCompaction "comp-1" s1))
    applySettle "comp-1" s1 |> isNone

// session-scoped late idle uses active continuation epoch
let sessionLateIdleUsesActiveContinuationEpoch () =
    let s =
        { freshSessionState with
            SessionGeneration = 5
            CancelGeneration = 3
            HumanTurnId = "ht-now"
            ActiveContinuationGen = 4
            ActiveContinuationCancelGen = 2 }

    let d =
        disposeContinuationSessionEvent true None false false false s

    equal "active epoch behind session is stale" LateEventDisposition.DiscardStaleEpoch d

    let live =
        { s with
            ActiveContinuationGen = 5
            ActiveContinuationCancelGen = 3 }

    let dLive =
        disposeContinuationSessionEvent true None false false false live

    equal "live epoch accepted" LateEventDisposition.Accept dLive

// tryTransition refuses cross-episode after lease swap
let tryTransitionRefusesSwappedContinuation () =
    let epoch =
        { SessionGeneration = 0
          CancelGeneration = 0
          HumanTurnId = "" }

    let leaseA = mkContinuationLease "a" 1 epoch LeaseStatus.Requested
    let leaseB = mkContinuationLease "b" 2 epoch LeaseStatus.Requested
    let s = freshSessionState |> withContinuation leaseB

    tryTransitionPendingLease "a" LeaseStatus.Requested LeaseStatus.DispatchStarted s
    |> isNone

    tryTransitionPendingLease "b" LeaseStatus.Requested LeaseStatus.DispatchStarted s
    |> isSome

let run () =
    lateIdleFromEpisodeACannotSettleEpisodeB ()
    generationBumpDiscardsStaleContinuation ()
    compactionSettleScopedToActiveEpisode ()
    compactionSettleRejectsStaleCancelGeneration ()
    sessionLateIdleUsesActiveContinuationEpoch ()
    tryTransitionRefusesSwappedContinuation ()
