namespace Wanxiangshu.Execution.Failure

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity

module Surface =

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    let private requiredText fieldName (value: obj) =
        if isNull value || not (isString value) then
            invalidArg fieldName $"missing {fieldName}"

        let parsed: string = unbox value

        if String.IsNullOrWhiteSpace parsed then
            invalidArg fieldName $"missing {fieldName}"

        parsed

    let private commitmentOf value =
        match requiredText "failure.commitment" value with
        | "NotCommitted" -> PersistenceCommitment.NotCommitted
        | "Committed" -> PersistenceCommitment.Committed
        | "Unknown" -> PersistenceCommitment.Unknown
        | commitment -> invalidArg "failure.commitment" $"unknown persistence commitment '{commitment}'"

    let private failureOf (value: obj) =
        let label =
            if isString value then
                requiredText "failure" value
            elif isNull value then
                invalidArg "failure" "missing failure"
            else
                requiredText "failure.kind" value?kind

        match label with
        | "LocalInvariant" -> ExecutionFailure.LocalInvariant
        | "ProtocolRejection" -> ExecutionFailure.ProtocolRejection
        | "AuthorizationDenied" -> ExecutionFailure.AuthorizationDenied
        | "UserCancelled" -> ExecutionFailure.UserCancelled
        | "Superseded" -> ExecutionFailure.Superseded
        | "CapacityQueueFull" -> ExecutionFailure.CapacityQueueFull
        | "ProviderTransient" -> ExecutionFailure.ProviderTransient
        | "ProviderPermanent" -> ExecutionFailure.ProviderPermanent
        | "AcceptanceUnknown" -> ExecutionFailure.AcceptanceUnknown
        | "StreamInterruptedAfterFirstToken" -> ExecutionFailure.StreamInterruptedAfterFirstToken
        | "PersistenceFailure" -> ExecutionFailure.PersistenceFailure(commitmentOf value?commitment)
        | failure -> invalidArg "failure" $"unknown execution failure '{failure}'"

    let private phaseOf value =
        match requiredText "phase" value with
        | "NoAcceptedFact" -> DurableExecutionLifecycle.NoAcceptedFact
        | "AcceptedBeforeProvider" -> DurableExecutionLifecycle.AcceptedBeforeProvider
        | "ProviderStarted" -> DurableExecutionLifecycle.ProviderStarted
        | "Terminal" -> DurableExecutionLifecycle.Terminal
        | phase -> invalidArg "phase" $"unknown durable execution phase '{phase}'"

    let private requestKindOf value =
        match requiredText "provider.requestKind" value with
        | "WorkMain" -> ProviderRequestKind.WorkMain
        | "BloggerMain" -> ProviderRequestKind.BloggerMain
        | "BloggerSquash" -> ProviderRequestKind.BloggerSquash
        | "InteractionRepair" -> ProviderRequestKind.InteractionRepair
        | "StrengthReplica" -> ProviderRequestKind.StrengthReplica
        | requestKind -> invalidArg "provider.requestKind" $"unknown provider request kind '{requestKind}'"

    let private budgetOf fieldName value =
        match requiredText fieldName value with
        | "Available" -> ProviderRecoveryBudget.Available
        | "Exhausted" -> ProviderRecoveryBudget.Exhausted
        | budget -> invalidArg fieldName $"unknown provider recovery budget '{budget}'"

    let private breakerOf value =
        match requiredText "provider.breaker" value with
        | "Closed" -> ProviderBreakerState.Closed
        | "Open" -> ProviderBreakerState.Open
        | breaker -> invalidArg "provider.breaker" $"unknown provider breaker state '{breaker}'"

    let private executionKeyOf (value: obj) : ChatExecutionKey =
        if isNull value then
            invalidArg "executionKey" "missing executionKey"

        { SessionId = SessionId.create (requiredText "executionKey.sessionId" value?sessionId)
          PhysicalUserMessageId =
            PhysicalUserMessageId.create (requiredText "executionKey.physicalUserMessageId" value?physicalUserMessageId) }

    let private capacityOf (value: obj) =
        if isNull value then
            CapacityOwnership.NoCapacityFence
        else
            requiredText "capacityFence.reference" value?reference
            |> box
            |> ExactCapacityFenceReference.Create
            |> CapacityOwnership.OwnsExactFence

    let private providerFactsOf (value: obj) =
        if isNull value then
            invalidArg "provider" "missing provider recovery facts"

        { LogicalRun = LogicalRunId.create (requiredText "provider.logicalRun" value?logicalRun)
          ProviderRun = ProviderRunIdentity.create (requiredText "provider.providerRun" value?providerRun)
          RequestKind = requestKindOf value?requestKind
          RetryBudget = budgetOf "provider.retryBudget" value?retryBudget
          FallbackBudget = budgetOf "provider.fallbackBudget" value?fallbackBudget
          Breaker = breakerOf value?breaker }

    let private inputOf (value: obj) =
        if isNull value then
            invalidArg "input" "missing execution failure policy input"

        { Failure = failureOf value?failure
          Lifecycle = phaseOf value?phase
          ExecutionKey = executionKeyOf value?executionKey
          Capacity = capacityOf value?capacityFence
          Provider = providerFactsOf value?provider }

    let private keyView (key: ChatExecutionKey) =
        box
            {| sessionId = SessionId.value key.SessionId
               physicalUserMessageId = PhysicalUserMessageId.value key.PhysicalUserMessageId |}

    let private requestKindLabel =
        function
        | ProviderRequestKind.WorkMain -> "WorkMain"
        | ProviderRequestKind.BloggerMain -> "BloggerMain"
        | ProviderRequestKind.BloggerSquash -> "BloggerSquash"
        | ProviderRequestKind.InteractionRepair -> "InteractionRepair"
        | ProviderRequestKind.StrengthReplica -> "StrengthReplica"

    let private authorizationView kind (authorization: ProviderRecoveryAuthorization) =
        box
            {| kind = kind
               decisionId = authorization.DecisionId.Value
               logicalRun = LogicalRunId.value authorization.LogicalRun
               providerRun = ProviderRunIdentity.value authorization.ProviderRun
               requestKind = requestKindLabel authorization.RequestKind |}

    let private retryView =
        function
        | RetryDecision.NoRetry -> box {| kind = "NoRetry" |}
        | RetryDecision.RetryFreshAttempt authorization -> authorizationView "RetryFreshAttempt" authorization

    let private fallbackView =
        function
        | FallbackDecision.NoFallback -> box {| kind = "NoFallback" |}
        | FallbackDecision.AdvanceFallback authorization -> authorizationView "AdvanceFallback" authorization

    let private breakerView =
        function
        | BreakerDecision.NoBreakerTransition -> box {| kind = "NoBreakerTransition" |}
        | BreakerDecision.RecordProviderTransientFailure -> box {| kind = "RecordProviderTransientFailure" |}
        | BreakerDecision.RecordProviderPermanentFailure -> box {| kind = "RecordProviderPermanentFailure" |}

    let private fenceReference (fence: ExactCapacityFenceReference) : string = unbox fence.Value

    let private capacityView =
        function
        | CapacitySettlement.NoCapacitySettlement -> box {| kind = "NoCapacitySettlement" |}
        | CapacitySettlement.RetainExactFence fence ->
            box
                {| kind = "RetainExactFence"
                   fenceReference = fenceReference fence |}
        | CapacitySettlement.ReleaseExactFence fence ->
            box
                {| kind = "ReleaseExactFence"
                   fenceReference = fenceReference fence |}

    let private terminalDispositionLabel =
        function
        | ChatExecutionTerminalDisposition.Completed -> "Completed"
        | ChatExecutionTerminalDisposition.Cancelled -> "Cancelled"
        | ChatExecutionTerminalDisposition.Rejected -> "Rejected"
        | ChatExecutionTerminalDisposition.Failed -> "Failed"

    let private messageView =
        function
        | MessageDisposition.KeepCurrentFact -> box {| kind = "KeepCurrentFact" |}
        | MessageDisposition.TerminalizeAcceptedPreProvider(key, disposition) ->
            box
                {| kind = "TerminalizeAcceptedPreProvider"
                   executionKey = keyView key
                   disposition = terminalDispositionLabel disposition |}
        | MessageDisposition.TerminalizeProviderStarted(key, disposition) ->
            box
                {| kind = "TerminalizeProviderStarted"
                   executionKey = keyView key
                   disposition = terminalDispositionLabel disposition |}
        | MessageDisposition.AwaitAcceptanceReconciliation key ->
            box
                {| kind = "AwaitAcceptanceReconciliation"
                   executionKey = keyView key |}

    let private fatalityView =
        function
        | FatalityDecision.NoFatality -> box {| kind = "NoFatality" |}
        | FatalityDecision.FatalAfterSettlement -> box {| kind = "FatalAfterSettlement" |}

    let decide (value: obj) : obj =
        let decision = value |> inputOf |> ExecutionFailurePolicy.decide

        box
            {| retry = retryView decision.Retry
               fallback = fallbackView decision.Fallback
               breaker = breakerView decision.Breaker
               capacitySettlement = capacityView decision.CapacitySettlement
               messageDisposition = messageView decision.MessageDisposition
               fatality = fatalityView decision.Fatality |}
