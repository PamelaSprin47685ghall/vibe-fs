module Wanxiangshu.Runtime.Fallback.SessionRuntimeTransitions

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.SessionRuntime

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

let private hasActiveContinuationLease (s: FallbackSessionRuntime) =
    match s.PendingLease with
    | Some lease ->
        match lease.Status with
        | LeaseStatus.Requested
        | LeaseStatus.DispatchStarted
        | LeaseStatus.Dispatched
        | LeaseStatus.Running -> true
        | LeaseStatus.Cancelled
        | LeaseStatus.Settled -> false
    | None -> false

/// A fallback continuation dispatch is being initiated — mark owner and gate, stamp generations.
let startDispatch
    (model: FallbackModel)
    (promptTextOpt: string option)
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime =
    if hasActiveContinuationLease s then
        s
    else
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
        ActiveGates = emptyActiveGates
        AbortUnavailable = s.AbortUnavailable }

/// A compaction run is starting — claim ownership, track its identity, and arm the summary-transform bypass.
let beginCompaction
    (compactionId: string)
    (compactionOrdinal: int)
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime =
    { s with
        CompactionActiveId = compactionId
        CompactionActiveOrdinal = compactionOrdinal
        CompactionHumanTurnId = s.HumanTurnId
        CompactionCancelGeneration = s.CancelGeneration
        CompactionGeneration = s.CompactionGeneration + 1
        CompactionSummaryTransformPending = true
        Owner = SessionOwner.Compaction }

/// A compaction run has settled — release ownership if compaction owned the session.
let settleCompaction (s: FallbackSessionRuntime) : FallbackSessionRuntime =
    { s with
        CompactionActiveId = ""
        CompactionActiveOrdinal = 0
        CompactionHumanTurnId = ""
        CompactionCancelGeneration = 0
        CompactionGeneration = 0
        CompactionCompacted = false
        CompactionContinuationObserved = false
        CompactionSummaryTransformPending = false
        Owner =
            if s.Owner = SessionOwner.Compaction then
                SessionOwner.NoOwner
            else
                s.Owner }
