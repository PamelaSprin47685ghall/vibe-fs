namespace Wanxiangshu.Execution.Failure

open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

[<RequireQualifiedAccess>]
type PersistenceCommitment =
    | NotCommitted
    | Committed
    | Unknown

[<RequireQualifiedAccess>]
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
type ExactCapacityFenceReference =
    member internal Value: obj
    static member internal Create: value: obj -> ExactCapacityFenceReference

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
      FallbackBudget: ProviderRecoveryBudget
      Breaker: ProviderBreakerState }

type ExecutionFailureInput =
    { Failure: ExecutionFailure
      Lifecycle: DurableExecutionLifecycle
      ExecutionKey: ChatExecutionKey
      Capacity: CapacityOwnership
      Provider: ProviderRecoveryFacts }

[<Sealed>]
type ProviderRecoveryDecisionId =
    member internal Value: string
    static member internal Create: value: string -> ProviderRecoveryDecisionId

[<Sealed>]
type ProviderRecoveryAuthorization =
    member DecisionId: ProviderRecoveryDecisionId
    member LogicalRun: LogicalRunId
    member ProviderRun: ProviderRunIdentity
    member RequestKind: ProviderRequestKind
    static member internal Create:
        decisionId: ProviderRecoveryDecisionId * logicalRun: LogicalRunId * providerRun: ProviderRunIdentity * requestKind: ProviderRequestKind ->
        ProviderRecoveryAuthorization

[<RequireQualifiedAccess>]
type RetryDecision =
    | NoRetry
    | RetryFreshAttempt of ProviderRecoveryAuthorization

[<RequireQualifiedAccess>]
type FallbackDecision =
    | NoFallback
    | AdvanceFallback of ProviderRecoveryAuthorization

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
type MessageDisposition =
    | KeepCurrentFact
    | TerminalizeAcceptedPreProvider of ChatExecutionKey * ChatExecutionTerminalDisposition
    | TerminalizeProviderStarted of ChatExecutionKey * ChatExecutionTerminalDisposition
    | AwaitAcceptanceReconciliation of ChatExecutionKey

[<RequireQualifiedAccess>]
type FatalityDecision =
    | NoFatality
    | FatalAfterSettlement

type ExecutionFailureDecision =
    { Retry: RetryDecision
      Fallback: FallbackDecision
      Breaker: BreakerDecision
      CapacitySettlement: CapacitySettlement
      MessageDisposition: MessageDisposition
      Fatality: FatalityDecision }
