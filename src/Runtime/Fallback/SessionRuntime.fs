module Wanxiangshu.Runtime.Fallback.SessionRuntime

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags

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
      AbortUnavailable: bool }

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
      AbortUnavailable = false }
