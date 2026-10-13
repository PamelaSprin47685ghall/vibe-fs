namespace Wanxiangshu.Mission.Lifecycle

[<RequireQualifiedAccess>]
type StageElement =
    | S1
    | S2
    | S3
    | S4

[<RequireQualifiedAccess>]
module StageElement =
    let render (stage: StageElement) =
        match stage with
        | StageElement.S1 -> "S1"
        | StageElement.S2 -> "S2"
        | StageElement.S3 -> "S3"
        | StageElement.S4 -> "S4"

    let parse (value: string) =
        match value with
        | "S1" -> Some StageElement.S1
        | "S2" -> Some StageElement.S2
        | "S3" -> Some StageElement.S3
        | "S4" -> Some StageElement.S4
        | _ -> None

[<RequireQualifiedAccess>]
type WorkSequence =
    | Plan
    | Execution

[<RequireQualifiedAccess>]
module WorkSequence =
    let elements (sequence: WorkSequence) =
        match sequence with
        | WorkSequence.Plan -> [ StageElement.S1; StageElement.S2; StageElement.S3 ]
        | WorkSequence.Execution -> [ StageElement.S4 ]

    let ofElements (elements: StageElement list) =
        match elements with
        | [ StageElement.S1; StageElement.S2; StageElement.S3 ] -> Some WorkSequence.Plan
        | [ StageElement.S4 ] -> Some WorkSequence.Execution
        | _ -> None

    let confirmationCount (sequence: WorkSequence) =
        match sequence with
        | WorkSequence.Plan -> 1
        | WorkSequence.Execution -> 2

    let render (sequence: WorkSequence) = elements sequence |> List.map StageElement.render

[<RequireQualifiedAccess>]
type RetirementOutcome =
    | Relayed
    | Delivered

[<RequireQualifiedAccess>]
module RetirementOutcome =
    let render (outcome: RetirementOutcome) =
        match outcome with
        | RetirementOutcome.Relayed -> "Relayed"
        | RetirementOutcome.Delivered -> "Delivered"

    let parse (value: string) =
        match value with
        | "Relayed" -> Some RetirementOutcome.Relayed
        | "Delivered" -> Some RetirementOutcome.Delivered
        | _ -> None

[<RequireQualifiedAccess>]
type ReviewOutcome =
    | Promoted
    | Stayed

[<RequireQualifiedAccess>]
module ReviewOutcome =
    let render (outcome: ReviewOutcome) =
        match outcome with
        | ReviewOutcome.Promoted -> "Promoted"
        | ReviewOutcome.Stayed -> "Stayed"

    let parse (value: string) =
        match value with
        | "Promoted" -> Some ReviewOutcome.Promoted
        | "Stayed" -> Some ReviewOutcome.Stayed
        | _ -> None

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

[<RequireQualifiedAccess>]
module LifecycleEventTypes =
    let WorkOpened = Wanxiangshu.Mission.Planning.LifecycleEventTypes.WorkOpened
    let DevOpsBound = Wanxiangshu.Mission.Planning.LifecycleEventTypes.DevOpsBound
    let IncumbencyOpened = Wanxiangshu.Mission.Planning.LifecycleEventTypes.IncumbencyOpened
    let ConfirmationCommitted = Wanxiangshu.Mission.Planning.LifecycleEventTypes.ConfirmationCommitted
    let IncumbencyRetired = Wanxiangshu.Mission.Planning.LifecycleEventTypes.IncumbencyRetired
    let Reviewed = Wanxiangshu.Mission.Planning.LifecycleEventTypes.Reviewed
    let Reopened = Wanxiangshu.Mission.Planning.LifecycleEventTypes.Reopened
    let AdvanceCommitted = Wanxiangshu.Mission.Planning.LifecycleEventTypes.AdvanceCommitted
    let RetirementBlocked = Wanxiangshu.Mission.Planning.LifecycleEventTypes.RetirementBlocked

    let all = Wanxiangshu.Mission.Planning.LifecycleEventTypes.all
    let isLifecycleEvent eventType =
        Wanxiangshu.Mission.Planning.LifecycleEventTypes.isLifecycleEvent eventType

module LifecycleIdentity =
    let spawnMark = "spawn-from-authorized-delivery-v1"

    let authorityInitial (sha256: string -> string) (workId: string) =
        sha256 ("authority-initial-v1\n" + workId)

    let private retirementFields
        (workId: string)
        (providerRunId: string)
        (toolCallId: string)
        (outcome: string)
        (expectedAdvance: string)
        =
        workId + "\n" + providerRunId + "\n" + toolCallId + "\n" + outcome + "\n" + expectedAdvance

    let oneshotRetirementId
        (sha256: string -> string)
        (workId: string)
        (providerRunId: string)
        (toolCallId: string)
        (outcome: string)
        (expectedAdvance: string)
        =
        sha256 ("oneshot-retirement-v1\n" + retirementFields workId providerRunId toolCallId outcome expectedAdvance)

    let executionRetirementId
        (sha256: string -> string)
        (workId: string)
        (providerRunId: string)
        (toolCallId: string)
        (outcome: string)
        (expectedAdvance: string)
        =
        sha256 ("retirement-v1\n" + retirementFields workId providerRunId toolCallId outcome expectedAdvance)

    let successorIncumbencyId (sha256: string -> string) (retirementId: string) =
        sha256 ("successor-incumbency-v1\n" + retirementId)

    let firstIncumbencyId (sha256: string -> string) (workId: string) =
        sha256 ("first-incumbency-v1\n" + workId)
