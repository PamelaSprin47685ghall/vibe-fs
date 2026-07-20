module Wanxiangshu.Runtime.Fallback.SessionRuntime

open System
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags

type EpisodeKind =
    | Human = 0
    | Nudge = 1
    | Fallback = 2
    | Compaction = 3
    | Title = 4

type ActiveEpisode =
    { EpisodeId: string
      SessionId: string
      Generation: int
      Kind: EpisodeKind
      LeaseId: string option
      HumanTurnId: string option
      StartedByEventId: string option }

type PendingLease =
    { ContinuationID: string
      ContinuationOrdinal: int
      SessionGeneration: int
      HumanTurnID: string
      HostUserMessageId: string
      HostRunId: string
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
      HostUserMessageId: string
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
      CompactionHumanTurnId: string
      CompactionCancelGeneration: int
      CompactionForceStopped: bool
      CompactionCompacted: bool
      CompactionContinuationObserved: bool
      CompactionGeneration: int
      CompactionSummaryTransformPending: bool
      ActiveContinuationGen: int
      ActiveContinuationCancelGen: int
      ActiveGates: Set<FallbackSessionGateFlag>
      AbortUnavailable: bool
      TerminalConsumed: bool
      ActiveEpisode: ActiveEpisode option }

let emptyActiveGates: Set<FallbackSessionGateFlag> =
    Set.empty<FallbackSessionGateFlag>

let freshState: SessionFallbackState =
    { Phase = FallbackPhase.Idle
      CurrentIndex = 0
      FailureCount = 0
      Lifecycle = FallbackLifecycle.Active
      ContinueCount = 0
      RecoveryCount = 0
      LastAssistantMessageId = "" }

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
      CompactionHumanTurnId = ""
      CompactionCancelGeneration = 0
      CompactionForceStopped = false
      CompactionCompacted = false
      CompactionContinuationObserved = false
      CompactionGeneration = 0
      CompactionSummaryTransformPending = false
      ActiveContinuationGen = 0
      ActiveContinuationCancelGen = 0
      ActiveGates = emptyActiveGates
      AbortUnavailable = false
      TerminalConsumed = false
      ActiveEpisode = None }

let beginHumanTurn (msgId: string) (s: FallbackSessionRuntime) : FallbackSessionRuntime =
    let nextOrdinal = s.HumanTurnOrdinal + 1
    let nextTurnId = "ht-" + string nextOrdinal + "-" + Guid.NewGuid().ToString("N")
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
            |> Set.remove FallbackSessionGateFlag.NudgeActive
        TerminalConsumed = false
        ActiveEpisode =
            Some
                { EpisodeId = "ep-human-" + nextTurnId
                  SessionId = ""
                  Generation = s.SessionGeneration
                  Kind = EpisodeKind.Human
                  LeaseId = None
                  HumanTurnId = Some nextTurnId
                  StartedByEventId = Some msgId } }

let private hasActiveContinuationLease (s: FallbackSessionRuntime) =
    match s.PendingLease with
    | Some lease ->
        match lease.Status with
        | LeaseStatus.Requested
        | LeaseStatus.DispatchStarted
        | LeaseStatus.AcceptanceUnknown
        | LeaseStatus.Dispatched
        | LeaseStatus.Running -> true
        | LeaseStatus.Cancelled
        | LeaseStatus.Settled -> false
    | None -> false

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
        let leaseId = Guid.NewGuid().ToString("N")

        { s with
            Owner = SessionOwner.Fallback
            PendingLease =
                Some
                    { ContinuationID = leaseId
                      ContinuationOrdinal = nextOrdinal
                      SessionGeneration = gen
                      HumanTurnID = s.HumanTurnId
                      HostUserMessageId = ""
                      HostRunId = ""
                      CancelGeneration = cancelGen
                      Owner = SessionOwner.Fallback
                      Model = model
                      PromptText = promptTextOpt
                      Status = LeaseStatus.Requested }
            ContinuationOrdinal = nextOrdinal
            ActiveContinuationGen = gen
            ActiveContinuationCancelGen = cancelGen
            ActiveGates = Set.add FallbackSessionGateFlag.MainContinuationAwaitingStart s.ActiveGates
            TerminalConsumed = false
            ActiveEpisode =
                Some
                    { EpisodeId = "ep-fallback-" + leaseId
                      SessionId = ""
                      Generation = gen
                      Kind = EpisodeKind.Fallback
                      LeaseId = Some leaseId
                      HumanTurnId = Some s.HumanTurnId
                      StartedByEventId = None } }

let completeDispatch (s: FallbackSessionRuntime) : FallbackSessionRuntime =
    { s with
        PendingNudgeLease = None
        ActiveGates =
            s.ActiveGates
            |> Set.remove FallbackSessionGateFlag.MainContinuationAwaitingStart
            |> Set.remove FallbackSessionGateFlag.EventHandlingActive
            |> Set.remove FallbackSessionGateFlag.NudgeActive }

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
        Owner = SessionOwner.Compaction
        TerminalConsumed = false
        ActiveEpisode =
            Some
                { EpisodeId = "ep-compaction-" + compactionId
                  SessionId = ""
                  Generation = s.SessionGeneration
                  Kind = EpisodeKind.Compaction
                  LeaseId = None
                  HumanTurnId = Some s.HumanTurnId
                  StartedByEventId = None } }

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
                s.Owner
        TerminalConsumed = false
        ActiveEpisode = None }
