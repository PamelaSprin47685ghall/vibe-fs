module Wanxiangshu.Runtime.Fallback.SessionRuntime

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.GateState

type PendingLease =
    { ContinuationID: string
      ContinuationOrdinal: int
      SessionGeneration: int
      HumanTurnID: string
      CancelGeneration: int
      Owner: SessionOwner
      Model: FallbackModel
      PromptText: string option
      Status: LeaseStatus }

type NudgeLease =
    { NudgeID: string
      NudgeOrdinal: int
      Nonce: string
      HumanTurnID: string
      SessionGeneration: int
      CancelGeneration: int
      Owner: SessionOwner
      Status: LeaseStatus }

type FallbackSessionRuntime =
    { Core: SessionFallbackState
      Chain: FallbackChain
      AgentName: string
      Model: FallbackModel option
      BusyCount: int
      Consumed: bool option
      InjectedModel: FallbackModel option
      InjectedAt: int64 option
      Owner: SessionOwner
      PendingLease: PendingLease option
      PendingNudgeLease: NudgeLease option
      ActiveNudgeNonce: string
      LatestHumanModel: string option
      SessionGeneration: int
      CancelGeneration: int
      HumanTurnOrdinal: int
      ContinuationOrdinal: int
      NudgeOrdinal: int
      CompactionOrdinal: int
      HumanTurnId: string
      LastHumanMessageId: string
      CompactionActiveId: string
      CompactionActiveOrdinal: int
      CompactionForceStopped: bool
      CompactionCompacted: bool
      CompactionContinuationObserved: bool
      CompactionGeneration: int
      CompactionSummaryTransformPending: bool
      ActiveContinuationGen: int
      ActiveContinuationCancelGen: int
      ActiveGates: Set<FallbackSessionGateFlag> }

let emptyActiveGates: Set<FallbackSessionGateFlag> =
    Set.empty<FallbackSessionGateFlag>

let freshState: SessionFallbackState =
    { Phase = FallbackPhase.Idle
      CurrentIndex = 0
      FailureCount = 0
      Lifecycle = FallbackLifecycle.Active
      ContinueCount = 0
      RecoveryCount = 0 }

let freshSessionState: FallbackSessionRuntime =
    { Core = freshState
      Chain = []
      AgentName = ""
      Model = None
      BusyCount = 0
      Consumed = None
      InjectedModel = None
      InjectedAt = None
      Owner = SessionOwner.NoOwner
      PendingLease = None
      PendingNudgeLease = None
      ActiveNudgeNonce = ""
      LatestHumanModel = None
      SessionGeneration = 0
      CancelGeneration = 0
      HumanTurnOrdinal = 0
      ContinuationOrdinal = 0
      NudgeOrdinal = 0
      CompactionOrdinal = 0
      HumanTurnId = ""
      LastHumanMessageId = ""
      CompactionActiveId = ""
      CompactionActiveOrdinal = 0
      CompactionForceStopped = false
      CompactionCompacted = false
      CompactionContinuationObserved = false
      CompactionGeneration = 0
      CompactionSummaryTransformPending = false
      ActiveContinuationGen = 0
      ActiveContinuationCancelGen = 0
      ActiveGates = emptyActiveGates }

// --- Unified domain transitions ---
// Each function captures a complete lifecycle transition atomically.
// These are the preferred mutation surface; callers should use them
// through FallbackRuntimeStore.Update/UpdateSession/UpdateSessionReturning.

/// A human turn has begun — reset per-turn state, set owner, clear lease gates,
/// and atomically increment the turn ordinal with a new turn ID.
let beginHumanTurn (msgId: string) (s: FallbackSessionRuntime) : FallbackSessionRuntime =
    let nextOrdinal = s.HumanTurnOrdinal + 1

    let nextTurnId =
        "ht-" + string nextOrdinal + "-" + System.Guid.NewGuid().ToString("N")

    let nextCancelGen = s.CancelGeneration + 1

    { s with
        Chain = []
        Model = None
        InjectedModel = None
        InjectedAt = None
        Owner = SessionOwner.Human
        PendingLease = None
        PendingNudgeLease = None
        ActiveNudgeNonce = ""
        HumanTurnId = nextTurnId
        HumanTurnOrdinal = nextOrdinal
        LastHumanMessageId = msgId
        CompactionForceStopped = false
        CancelGeneration = nextCancelGen
        ActiveContinuationGen = s.SessionGeneration
        ActiveContinuationCancelGen = nextCancelGen
        ActiveGates =
            s.ActiveGates
            |> Set.remove FallbackSessionGateFlag.EventHandlingActive
            |> Set.remove FallbackSessionGateFlag.NudgeActive }

/// A fallback continuation dispatch is being initiated — mark owner and gate, stamp generations.
let startDispatch
    (model: FallbackModel)
    (promptTextOpt: string option)
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime =
    let gen = s.SessionGeneration
    let cancelGen = s.CancelGeneration
    let nextOrdinal = s.ContinuationOrdinal + 1

    { s with
        Owner = SessionOwner.Fallback
        PendingLease =
            Some
                { ContinuationID = System.Guid.NewGuid().ToString("N")
                  ContinuationOrdinal = nextOrdinal
                  SessionGeneration = gen
                  HumanTurnID = s.HumanTurnId
                  CancelGeneration = cancelGen
                  Owner = SessionOwner.Fallback
                  Model = model
                  PromptText = promptTextOpt
                  Status = LeaseStatus.Requested }
        ContinuationOrdinal = nextOrdinal
        ActiveContinuationGen = gen
        ActiveContinuationCancelGen = cancelGen
        ActiveGates = Set.add FallbackSessionGateFlag.MainContinuationAwaitingStart s.ActiveGates }

/// A dispatched continuation lease has been fully acknowledged — clear lease and nudge gates.
let completeDispatch (s: FallbackSessionRuntime) : FallbackSessionRuntime =
    { s with
        PendingNudgeLease = None
        ActiveGates =
            s.ActiveGates
            |> Set.remove FallbackSessionGateFlag.MainContinuationAwaitingStart
            |> Set.remove FallbackSessionGateFlag.EventHandlingActive
            |> Set.remove FallbackSessionGateFlag.NudgeActive }

/// The episode is ending (user abort / task-complete / new human turn).
let cancelEpisode (s: FallbackSessionRuntime) : FallbackSessionRuntime =
    { freshSessionState with
        SessionGeneration = s.SessionGeneration
        CancelGeneration = s.CancelGeneration
        HumanTurnOrdinal = s.HumanTurnOrdinal
        ContinuationOrdinal = s.ContinuationOrdinal
        NudgeOrdinal = s.NudgeOrdinal
        CompactionOrdinal = s.CompactionOrdinal
        HumanTurnId = s.HumanTurnId
        LastHumanMessageId = s.LastHumanMessageId
        LatestHumanModel = s.LatestHumanModel
        Chain = s.Chain
        AgentName = s.AgentName
        Model = s.Model
        ActiveGates = emptyActiveGates }

/// A compaction run is starting — claim ownership, track its identity, and arm the summary-transform bypass.
let beginCompaction
    (compactionId: string)
    (compactionOrdinal: int)
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime =
    { s with
        CompactionActiveId = compactionId
        CompactionActiveOrdinal = compactionOrdinal
        CompactionGeneration = s.CompactionGeneration + 1
        CompactionSummaryTransformPending = true
        Owner = SessionOwner.Compaction }

/// A compaction run has settled — release ownership if compaction owned the session.
let settleCompaction (s: FallbackSessionRuntime) : FallbackSessionRuntime =
    { s with
        CompactionActiveId = ""
        CompactionActiveOrdinal = 0
        CompactionGeneration = 0
        CompactionCompacted = false
        CompactionContinuationObserved = false
        CompactionSummaryTransformPending = false
        Owner =
            if s.Owner = SessionOwner.Compaction then
                SessionOwner.NoOwner
            else
                s.Owner }
