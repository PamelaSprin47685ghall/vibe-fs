namespace Wanxiangshu.Execution.Failure

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module ExecutionFailurePolicy =

    let private noProviderRecovery = RetryDecision.NoRetry, FallbackDecision.NoFallback

    let private requestKindIdentity =
        function
        | ProviderRequestKind.WorkMain -> "WorkMain"
        | ProviderRequestKind.BloggerMain -> "BloggerMain"
        | ProviderRequestKind.BloggerSquash -> "BloggerSquash"
        | ProviderRequestKind.InteractionRepair -> "InteractionRepair"
        | ProviderRequestKind.StrengthReplica -> "StrengthReplica"

    let private decisionId (facts: ProviderRecoveryFacts) =
        [ LogicalRunId.value facts.LogicalRun
          ProviderRunIdentity.value facts.ProviderRun
          requestKindIdentity facts.RequestKind ]
        |> List.map (fun value -> $"{value.Length}:{value}")
        |> String.concat "|"
        |> ProviderRecoveryDecisionId.Create

    let private authorization (facts: ProviderRecoveryFacts) : ProviderRecoveryAuthorization =
        ProviderRecoveryAuthorization.Create(decisionId facts, facts.LogicalRun, facts.ProviderRun, facts.RequestKind)

    let private requestCanRecover (requestKind: ProviderRequestKind) =
        match requestKind with
        | ProviderRequestKind.WorkMain
        | ProviderRequestKind.BloggerMain
        | ProviderRequestKind.BloggerSquash
        | ProviderRequestKind.InteractionRepair -> true
        | ProviderRequestKind.StrengthReplica -> false

    let private transientRecovery (facts: ProviderRecoveryFacts) =
        let licence = authorization facts

        match facts.Breaker, facts.RetryBudget, facts.FallbackBudget with
        | ProviderBreakerState.Closed, ProviderRecoveryBudget.Available, ProviderRecoveryBudget.Available
        | ProviderBreakerState.Closed, ProviderRecoveryBudget.Available, ProviderRecoveryBudget.Exhausted ->
            RetryDecision.RetryFreshAttempt licence, FallbackDecision.NoFallback
        | ProviderBreakerState.Closed, ProviderRecoveryBudget.Exhausted, ProviderRecoveryBudget.Available
        | ProviderBreakerState.Open, ProviderRecoveryBudget.Available, ProviderRecoveryBudget.Available
        | ProviderBreakerState.Open, ProviderRecoveryBudget.Exhausted, ProviderRecoveryBudget.Available ->
            RetryDecision.NoRetry, FallbackDecision.AdvanceFallback licence
        | ProviderBreakerState.Closed, ProviderRecoveryBudget.Exhausted, ProviderRecoveryBudget.Exhausted
        | ProviderBreakerState.Open, ProviderRecoveryBudget.Available, ProviderRecoveryBudget.Exhausted
        | ProviderBreakerState.Open, ProviderRecoveryBudget.Exhausted, ProviderRecoveryBudget.Exhausted ->
            noProviderRecovery

    let private permanentRecovery (facts: ProviderRecoveryFacts) =
        match facts.FallbackBudget with
        | ProviderRecoveryBudget.Available ->
            RetryDecision.NoRetry, FallbackDecision.AdvanceFallback(authorization facts)
        | ProviderRecoveryBudget.Exhausted -> noProviderRecovery

    let private transientRecoveryFor
        (lifecycle: DurableExecutionLifecycle)
        (facts: ProviderRecoveryFacts)
        : RetryDecision * FallbackDecision =
        match lifecycle, requestCanRecover facts.RequestKind with
        | DurableExecutionLifecycle.ProviderStarted, true -> transientRecovery facts
        | DurableExecutionLifecycle.ProviderStarted, false
        | DurableExecutionLifecycle.NoAcceptedFact, true
        | DurableExecutionLifecycle.NoAcceptedFact, false
        | DurableExecutionLifecycle.AcceptedBeforeProvider, true
        | DurableExecutionLifecycle.AcceptedBeforeProvider, false
        | DurableExecutionLifecycle.Terminal, true
        | DurableExecutionLifecycle.Terminal, false -> noProviderRecovery

    let private permanentRecoveryFor
        (lifecycle: DurableExecutionLifecycle)
        (facts: ProviderRecoveryFacts)
        : RetryDecision * FallbackDecision =
        match lifecycle, requestCanRecover facts.RequestKind with
        | DurableExecutionLifecycle.ProviderStarted, true -> permanentRecovery facts
        | DurableExecutionLifecycle.ProviderStarted, false
        | DurableExecutionLifecycle.NoAcceptedFact, true
        | DurableExecutionLifecycle.NoAcceptedFact, false
        | DurableExecutionLifecycle.AcceptedBeforeProvider, true
        | DurableExecutionLifecycle.AcceptedBeforeProvider, false
        | DurableExecutionLifecycle.Terminal, true
        | DurableExecutionLifecycle.Terminal, false -> noProviderRecovery

    let private releaseCapacity lifecycle capacity =
        match lifecycle, capacity with
        | DurableExecutionLifecycle.NoAcceptedFact, CapacityOwnership.NoCapacityFence
        | DurableExecutionLifecycle.AcceptedBeforeProvider, CapacityOwnership.NoCapacityFence
        | DurableExecutionLifecycle.ProviderStarted, CapacityOwnership.NoCapacityFence
        | DurableExecutionLifecycle.Terminal, CapacityOwnership.NoCapacityFence ->
            CapacitySettlement.NoCapacitySettlement
        | DurableExecutionLifecycle.NoAcceptedFact, CapacityOwnership.OwnsExactFence _ownedFence ->
            CapacitySettlement.NoCapacitySettlement
        | DurableExecutionLifecycle.AcceptedBeforeProvider, CapacityOwnership.OwnsExactFence fence
        | DurableExecutionLifecycle.ProviderStarted, CapacityOwnership.OwnsExactFence fence
        | DurableExecutionLifecycle.Terminal, CapacityOwnership.OwnsExactFence fence ->
            CapacitySettlement.ReleaseExactFence fence

    let private retainCapacity capacity =
        match capacity with
        | CapacityOwnership.NoCapacityFence -> CapacitySettlement.NoCapacitySettlement
        | CapacityOwnership.OwnsExactFence fence -> CapacitySettlement.RetainExactFence fence

    let private terminalMessage key disposition lifecycle =
        match lifecycle with
        | DurableExecutionLifecycle.NoAcceptedFact
        | DurableExecutionLifecycle.Terminal -> MessageDisposition.KeepCurrentFact
        | DurableExecutionLifecycle.AcceptedBeforeProvider ->
            MessageDisposition.TerminalizeAcceptedPreProvider(key, disposition)
        | DurableExecutionLifecycle.ProviderStarted -> MessageDisposition.TerminalizeProviderStarted(key, disposition)

    let private providerMessage key phase retry fallback =
        match retry, fallback with
        | RetryDecision.RetryFreshAttempt _authorization, FallbackDecision.NoFallback ->
            MessageDisposition.KeepCurrentFact
        | RetryDecision.NoRetry, FallbackDecision.AdvanceFallback _authorization -> MessageDisposition.KeepCurrentFact
        | RetryDecision.NoRetry, FallbackDecision.NoFallback ->
            terminalMessage key ChatExecutionTerminalDisposition.Failed phase
        | RetryDecision.RetryFreshAttempt _retryAuthorization, FallbackDecision.AdvanceFallback _fallbackAuthorization ->
            terminalMessage key ChatExecutionTerminalDisposition.Failed phase

    let private ordinaryDecision
        (input: ExecutionFailureInput)
        (disposition: ChatExecutionTerminalDisposition)
        (fatality: FatalityDecision)
        : ExecutionFailureDecision =
        { Retry = RetryDecision.NoRetry
          Fallback = FallbackDecision.NoFallback
          Breaker = BreakerDecision.NoBreakerTransition
          CapacitySettlement = releaseCapacity input.Lifecycle input.Capacity
          MessageDisposition = terminalMessage input.ExecutionKey disposition input.Lifecycle
          Fatality = fatality }

    let private providerDecision
        (input: ExecutionFailureInput)
        (recovery: RetryDecision * FallbackDecision)
        (breaker: BreakerDecision)
        : ExecutionFailureDecision =
        let retry, fallback = recovery

        { Retry = retry
          Fallback = fallback
          Breaker = breaker
          CapacitySettlement = releaseCapacity input.Lifecycle input.Capacity
          MessageDisposition = providerMessage input.ExecutionKey input.Lifecycle retry fallback
          Fatality = FatalityDecision.NoFatality }

    let decide (input: ExecutionFailureInput) : ExecutionFailureDecision =
        match input.Failure with
        | ExecutionFailure.LocalInvariant ->
            ordinaryDecision input ChatExecutionTerminalDisposition.Failed FatalityDecision.FatalAfterSettlement
        | ExecutionFailure.ProtocolRejection ->
            ordinaryDecision input ChatExecutionTerminalDisposition.Rejected FatalityDecision.NoFatality
        | ExecutionFailure.AuthorizationDenied ->
            ordinaryDecision input ChatExecutionTerminalDisposition.Rejected FatalityDecision.NoFatality
        | ExecutionFailure.UserCancelled ->
            ordinaryDecision input ChatExecutionTerminalDisposition.Cancelled FatalityDecision.NoFatality
        | ExecutionFailure.Superseded ->
            ordinaryDecision input ChatExecutionTerminalDisposition.Cancelled FatalityDecision.NoFatality
        | ExecutionFailure.CapacityQueueFull ->
            ordinaryDecision input ChatExecutionTerminalDisposition.Failed FatalityDecision.NoFatality
        | ExecutionFailure.ProviderTransient ->
            providerDecision
                input
                (transientRecoveryFor input.Lifecycle input.Provider)
                BreakerDecision.RecordProviderTransientFailure
        | ExecutionFailure.ProviderPermanent ->
            providerDecision
                input
                (permanentRecoveryFor input.Lifecycle input.Provider)
                BreakerDecision.RecordProviderPermanentFailure
        | ExecutionFailure.AcceptanceUnknown ->
            { Retry = RetryDecision.NoRetry
              Fallback = FallbackDecision.NoFallback
              Breaker = BreakerDecision.NoBreakerTransition
              CapacitySettlement = retainCapacity input.Capacity
              MessageDisposition = MessageDisposition.AwaitAcceptanceReconciliation input.ExecutionKey
              Fatality = FatalityDecision.NoFatality }
        | ExecutionFailure.StreamInterruptedAfterFirstToken ->
            ordinaryDecision input ChatExecutionTerminalDisposition.Failed FatalityDecision.NoFatality
        | ExecutionFailure.PersistenceFailure PersistenceCommitment.NotCommitted ->
            { Retry = RetryDecision.NoRetry
              Fallback = FallbackDecision.NoFallback
              Breaker = BreakerDecision.NoBreakerTransition
              CapacitySettlement = retainCapacity input.Capacity
              MessageDisposition = MessageDisposition.KeepCurrentFact
              Fatality = FatalityDecision.NoFatality }
        | ExecutionFailure.PersistenceFailure PersistenceCommitment.Committed ->
            { Retry = RetryDecision.NoRetry
              Fallback = FallbackDecision.NoFallback
              Breaker = BreakerDecision.NoBreakerTransition
              CapacitySettlement = releaseCapacity input.Lifecycle input.Capacity
              MessageDisposition = MessageDisposition.KeepCurrentFact
              Fatality = FatalityDecision.FatalAfterSettlement }
        | ExecutionFailure.PersistenceFailure PersistenceCommitment.Unknown ->
            { Retry = RetryDecision.NoRetry
              Fallback = FallbackDecision.NoFallback
              Breaker = BreakerDecision.NoBreakerTransition
              CapacitySettlement = retainCapacity input.Capacity
              MessageDisposition = MessageDisposition.AwaitAcceptanceReconciliation input.ExecutionKey
              Fatality = FatalityDecision.FatalAfterSettlement }
