namespace Wanxiangshu.Mission.Lifecycle

[<RequireQualifiedAccess>]
type StageElement =
    | S1
    | S2
    | S3
    | S4

[<RequireQualifiedAccess>]
module StageElement =
    val render: StageElement -> string
    val parse: string -> StageElement option

[<RequireQualifiedAccess>]
type WorkSequence =
    | Plan
    | Execution

[<RequireQualifiedAccess>]
module WorkSequence =
    val elements: WorkSequence -> StageElement list
    val ofElements: StageElement list -> WorkSequence option
    val confirmationCount: WorkSequence -> int
    val render: WorkSequence -> string list

[<RequireQualifiedAccess>]
type RetirementOutcome =
    | Relayed
    | Delivered

[<RequireQualifiedAccess>]
module RetirementOutcome =
    val render: RetirementOutcome -> string
    val parse: string -> RetirementOutcome option

[<RequireQualifiedAccess>]
type ReviewOutcome =
    | Promoted
    | Stayed

[<RequireQualifiedAccess>]
module ReviewOutcome =
    val render: ReviewOutcome -> string
    val parse: string -> ReviewOutcome option

type ProjectionCut =
    { ProviderRunId: string
      ToolCallId: string }

[<RequireQualifiedAccess>]
type OpeningKind =
    | UserPlan
    | ManagerDirect
    | SpawnedExecution

[<RequireQualifiedAccess>]
type BindTrigger =
    | Ask
    | Resume
    | Horizon

type OneshotRetirement =
    { RetirementId: string
      IncumbencyId: string
      Outcome: RetirementOutcome
      ProviderRunId: string
      ToolCallId: string
      ExpectedAdvance: string
      RetirementCursor: int64
      ExecSessionId: string option
      Digest: string option
      Path: string option }

type ExecutionRetirement =
    { RetirementId: string
      IncumbencyId: string
      Outcome: RetirementOutcome
      ProjectionCut: ProjectionCut
      ExpectedAdvance: string }

[<RequireQualifiedAccess>]
type LifecycleEvent =
    | WorkOpened of workId: string * root: string * sequence: WorkSequence * authorityInitial: string
    | DevOpsBound of workId: string * devopsId: string * target: string option
    | IncumbencyOpened of workId: string * incumbencyId: string * stage: StageElement * openingCursor: int64
    | ConfirmationCommitted of incumbencyId: string * providerRunId: string * toolCallId: string
    | IncumbencyRetiredOneshot of OneshotRetirement
    | IncumbencyRetiredExecution of ExecutionRetirement
    | Reviewed of incumbencyId: string * stage: StageElement * outcome: ReviewOutcome
    | Reopened of incumbencyId: string
    | AdvanceCommitted of incumbencyId: string * expected: string * next: string
    | RetirementBlocked of incumbencyId: string * blockerDigest: string

module LifecycleIdentity =
    val spawnMark: string
    val authorityInitial: (string -> string) -> workId: string -> string

    val oneshotRetirementId:
        (string -> string) ->
        workId: string ->
        providerRunId: string ->
        toolCallId: string ->
        outcome: string ->
        expectedAdvance: string ->
            string

    val executionRetirementId:
        (string -> string) ->
        workId: string ->
        providerRunId: string ->
        toolCallId: string ->
        outcome: string ->
        expectedAdvance: string ->
            string

    val successorIncumbencyId: (string -> string) -> retirementId: string -> string
    val firstIncumbencyId: (string -> string) -> workId: string -> string
