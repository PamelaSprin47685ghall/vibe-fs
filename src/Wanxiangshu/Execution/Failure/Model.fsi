namespace Wanxiangshu.Execution.Failure

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
      Breaker: ProviderBreakerState }

[<Sealed>]
type ProviderRecoveryDecisionId =
    member internal Value: string
    static member internal Create: value: string -> ProviderRecoveryDecisionId

[<Sealed>]
type ProviderRecoveryAuthorization =
    member internal DecisionId: ProviderRecoveryDecisionId
    member internal LogicalRun: LogicalRunId
    member internal ProviderRun: ProviderRunIdentity
    member internal RequestKind: ProviderRequestKind

    static member internal Create:
        decisionId: ProviderRecoveryDecisionId *
        logicalRun: LogicalRunId *
        providerRun: ProviderRunIdentity *
        requestKind: ProviderRequestKind ->
            ProviderRecoveryAuthorization

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
