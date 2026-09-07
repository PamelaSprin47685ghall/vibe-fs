namespace Wanxiangshu.Execution.Failure

open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type PersistenceCommitment =
    | NotCommitted
    | Committed
    | Unknown

[<RequireQualifiedAccess>]
/// DSL-class: Evidence
type ExecutionFailure =
    | LocalInvariant
    | ProtocolRejection
    | AuthorizationDenied
    | UserCancelled
    | Superseded
    | CapacityQueueFull
    | ProviderTransient
    | ProviderPermanent
    | AcceptanceUnknown
    | StreamInterruptedAfterFirstToken
    | PersistenceFailure of PersistenceCommitment

[<RequireQualifiedAccess>]
type DurableExecutionLifecycle =
    | NoAcceptedFact
    | AcceptedBeforeProvider
    | ProviderStarted
    | Terminal

[<Sealed>]
type ExactCapacityFenceReference private (value: obj) =
    member internal _.Value = value
    static member internal Create(value: obj) = ExactCapacityFenceReference(value)

[<RequireQualifiedAccess>]
type CapacityOwnership =
    | NoCapacityFence
    | OwnsExactFence of ExactCapacityFenceReference

[<RequireQualifiedAccess>]
type ProviderRecoveryBudget =
    | Available
    | Exhausted

[<RequireQualifiedAccess>]
type ProviderBreakerState =
    | Closed
    | Open

type ProviderRecoveryFacts =
    { LogicalRun: LogicalRunId
      ProviderRun: ProviderRunIdentity
      RequestKind: ProviderRequestKind
      RetryBudget: ProviderRecoveryBudget
      Breaker: ProviderBreakerState }

type ExecutionFailureInput =
    { Failure: ExecutionFailure
      Lifecycle: DurableExecutionLifecycle
      ExecutionKey: ChatExecutionKey
      Capacity: CapacityOwnership
      Provider: ProviderRecoveryFacts }

[<Sealed>]
type ProviderRecoveryDecisionId private (value: string) =
    member _.Value = value
    static member internal Create(value: string) = ProviderRecoveryDecisionId(value)

[<Sealed>]
type ProviderRecoveryAuthorization
    private
    (
        decisionId: ProviderRecoveryDecisionId,
        logicalRun: LogicalRunId,
        providerRun: ProviderRunIdentity,
        requestKind: ProviderRequestKind
    ) =
    member _.DecisionId = decisionId
    member _.LogicalRun = logicalRun
    member _.ProviderRun = providerRun
    member _.RequestKind = requestKind

    static member internal Create(decisionId, logicalRun, providerRun, requestKind) =
        ProviderRecoveryAuthorization(decisionId, logicalRun, providerRun, requestKind)

[<RequireQualifiedAccess>]
type BreakerDecision =
    | NoBreakerTransition
    | RecordProviderTransientFailure
    | RecordProviderPermanentFailure

[<RequireQualifiedAccess>]
type CapacitySettlement =
    | NoCapacitySettlement
    | RetainExactFence of ExactCapacityFenceReference
    | ReleaseExactFence of ExactCapacityFenceReference

[<RequireQualifiedAccess>]
type ExecutionFailureResolution =
    | PreserveCurrentFact
    | AwaitAcceptanceReconciliation of ChatExecutionKey
    | RetryFreshAttempt of ProviderRecoveryAuthorization
    | TerminalizeAcceptedPreProvider of ChatExecutionKey * ChatExecutionTerminalDisposition
    | TerminalizeProviderStarted of ChatExecutionKey * ChatExecutionTerminalDisposition

[<RequireQualifiedAccess>]
type FatalityDecision =
    | NoFatality
    | FatalAfterSettlement

type ExecutionFailureDecision =
    { Resolution: ExecutionFailureResolution
      Breaker: BreakerDecision
      CapacitySettlement: CapacitySettlement
      Fatality: FatalityDecision }
