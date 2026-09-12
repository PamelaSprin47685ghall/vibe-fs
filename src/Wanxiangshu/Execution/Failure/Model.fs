namespace Wanxiangshu.Execution.Failure

open Wanxiangshu.Participant.Provider.Attempt
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

[<Sealed>]
type ProviderRecoveryDecisionId private (value: string) =
    member internal _.Value = value
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
    member internal _.DecisionId = decisionId
    member internal _.LogicalRun = logicalRun
    member internal _.ProviderRun = providerRun
    member internal _.RequestKind = requestKind

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
type FatalityDecision =
    | NoFatality
    | FatalAfterSettlement

