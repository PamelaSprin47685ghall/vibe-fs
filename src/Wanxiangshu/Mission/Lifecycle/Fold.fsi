namespace Wanxiangshu.Mission.Lifecycle

type ReviewStamp =
    { Stage: StageElement
      Outcome: ReviewOutcome }

type ActiveIncumbency =
    { Id: string
      OpeningStage: StageElement
      OpeningCursor: int64
      Review: ReviewStamp option
      Confirmed: (string * string) option
      CleanupBlockerDigest: string option }

type RetirementRecord =
    { RetirementId: string
      IncumbencyId: string
      Outcome: RetirementOutcome
      ExpectedAdvance: string
      ProviderRunId: string option
      ToolCallId: string option
      OpeningCursor: int64
      RetirementCursor: int64 option
      ProjectionCut: ProjectionCut option
      ExecSessionId: string option
      Digest: string option
      Path: string option
      Stage: StageElement option
      Review: ReviewStamp option }

type LifecycleWork =
    { WorkId: string
      Root: string
      Sequence: WorkSequence
      Authority: string
      AdvanceHistory: string list
      Active: ActiveIncumbency option
      CurrentDelivered: bool
      CurrentRelayed: bool
      PendingRelay: bool
      DeliveredRecord: RetirementRecord option
      DevOps: (string * string option) option
      Retirements: RetirementRecord list }

[<RequireQualifiedAccess>]
type RecoveryPosition =
    | PairIncomplete
    | Rejected
    | Conflict
    | DeliveredIdempotent
    | ActiveRebound
    | Nothing

[<RequireQualifiedAccess>]
type RecoveryNext =
    | StopPairIncomplete
    | StopRejected
    | StopConflict
    | StopDelivered
    | StopNothing
    | WaitForUserPrompt
    | EnterSuccessorPrompt
    | EnterDispatch

type FoldedWork =
    { Work: LifecycleWork
      Projection: StageElement
      DeliveryAuthorized: bool }

[<RequireQualifiedAccess>]
module LifecycleFold =
    val fresh: string -> string -> WorkSequence -> string -> LifecycleWork
    val projection: LifecycleWork -> Result<StageElement * bool, string>
    val apply: LifecycleEvent -> LifecycleWork -> Result<LifecycleWork, string>
    val applyAll: LifecycleEvent list -> Result<LifecycleWork, string>
    val folded: LifecycleWork -> Result<FoldedWork, string>
    val recover: LifecycleWork -> Result<RecoveryPosition * RecoveryNext, string>
