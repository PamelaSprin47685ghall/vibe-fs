namespace Wanxiangshu.Execution.Failure

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity

/// The decision of one exact execution failure is owned by `wanxiangli/failure`
/// (CUT-14 ①). This module only maps the host's typed facts to the library's
/// plain input and its plain decision back; the execution key and the exact
/// capacity fence travel by reference.
[<RequireQualifiedAccess>]
module ExecutionFailurePolicy =

    type private FailureLibrary =
        abstract decide: input: obj -> obj

    [<Import("ExecutionFailurePolicy", "wanxiangli/failure")>]
    let private failureLibraryClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : FailureLibrary = jsNative

    let private library = construct failureLibraryClass

    let private commitmentLabel =
        function
        | PersistenceCommitment.NotCommitted -> "NotCommitted"
        | PersistenceCommitment.Committed -> "Committed"
        | PersistenceCommitment.Unknown -> "Unknown"
        | PersistenceCommitment.NoNewWrite -> "NoNewWrite"

    let private failureView =
        function
        | ExecutionFailure.LocalInvariant -> box "LocalInvariant"
        | ExecutionFailure.ProtocolRejection -> box "ProtocolRejection"
        | ExecutionFailure.AuthorizationDenied -> box "AuthorizationDenied"
        | ExecutionFailure.UserCancelled -> box "UserCancelled"
        | ExecutionFailure.Superseded -> box "Superseded"
        | ExecutionFailure.CapacityQueueFull -> box "CapacityQueueFull"
        | ExecutionFailure.ProviderTransient -> box "ProviderTransient"
        | ExecutionFailure.ProviderPermanent -> box "ProviderPermanent"
        | ExecutionFailure.AcceptanceUnknown -> box "AcceptanceUnknown"
        | ExecutionFailure.StreamInterruptedAfterFirstToken -> box "StreamInterruptedAfterFirstToken"
        | ExecutionFailure.PersistenceFailure commitment ->
            createObj [ "kind" ==> "PersistenceFailure"; "commitment" ==> commitmentLabel commitment ]

    let private phaseLabel =
        function
        | DurableExecutionLifecycle.NoAcceptedFact -> "NoAcceptedFact"
        | DurableExecutionLifecycle.AcceptedBeforeProvider -> "AcceptedBeforeProvider"
        | DurableExecutionLifecycle.ProviderStarted -> "ProviderStarted"
        | DurableExecutionLifecycle.Terminal -> "Terminal"

    let private requestKindLabel =
        function
        | ProviderRequestKind.WorkMain -> "WorkMain"
        | ProviderRequestKind.InteractionRepair -> "InteractionRepair"

    let private providerView (facts: ProviderRecoveryFacts) =
        createObj
            [ "logicalRun" ==> LogicalRunId.value facts.LogicalRun
              "providerRun" ==> ProviderRunIdentity.value facts.ProviderRun
              "requestKind" ==> requestKindLabel facts.RequestKind
              "retryBudget"
              ==> (match facts.RetryBudget with
                   | ProviderRecoveryBudget.Available -> "Available"
                   | ProviderRecoveryBudget.Exhausted -> "Exhausted")
              "breaker"
              ==> (match facts.Breaker with
                   | ProviderBreakerState.Closed -> "Closed"
                   | ProviderBreakerState.Open -> "Open") ]

    let private fenceView =
        function
        | CapacityOwnership.NoCapacityFence -> null
        | CapacityOwnership.OwnsExactFence fence -> box fence

    let private unexpected (what: string) (value: obj) : 'T =
        failwithf "wanxiangli/failure returned an unknown %s '%A'" what value

    let private dispositionOf (value: obj) =
        match unbox<string> value with
        | "Cancelled" -> ChatExecutionTerminalDisposition.Cancelled
        | "Rejected" -> ChatExecutionTerminalDisposition.Rejected
        | "Failed" -> ChatExecutionTerminalDisposition.Failed
        | _ -> unexpected "terminal disposition" value

    let private decisionOf (key: ChatExecutionKey) (provider: ProviderRecoveryFacts option) (d: obj) =
        let resolution =
            match unbox<string> d?resolution with
            | "PreserveCurrentFact" -> ExecutionFailureResolution.PreserveCurrentFact
            | "AwaitAcceptanceReconciliation" -> ExecutionFailureResolution.AwaitAcceptanceReconciliation key
            | "RetryFreshAttempt" ->
                match provider with
                | Some facts ->
                    ExecutionFailureResolution.RetryFreshAttempt(
                        ProviderRecoveryAuthorization.Create(
                            ProviderRecoveryDecisionId.Create(unbox<string> d?authorization?decisionId),
                            facts.LogicalRun,
                            facts.ProviderRun,
                            facts.RequestKind
                        )
                    )
                | None -> unexpected "retry without provider facts" d?resolution
            | "TerminalizeAcceptedPreProvider" ->
                ExecutionFailureResolution.TerminalizeAcceptedPreProvider(key, dispositionOf d?terminalDisposition)
            | "TerminalizeProviderStarted" ->
                ExecutionFailureResolution.TerminalizeProviderStarted(key, dispositionOf d?terminalDisposition)
            | _ -> unexpected "resolution" d?resolution

        let breaker =
            match unbox<string> d?breaker with
            | "NoBreakerTransition" -> BreakerDecision.NoBreakerTransition
            | "RecordProviderTransientFailure" -> BreakerDecision.RecordProviderTransientFailure
            | "RecordProviderPermanentFailure" -> BreakerDecision.RecordProviderPermanentFailure
            | _ -> unexpected "breaker decision" d?breaker

        let capacity =
            match unbox<string> d?capacitySettlement with
            | "NoCapacitySettlement" -> CapacitySettlement.NoCapacitySettlement
            | "RetainExactFence" -> CapacitySettlement.RetainExactFence(unbox<ExactCapacityFenceReference> d?capacityFence)
            | "ReleaseExactFence" -> CapacitySettlement.ReleaseExactFence(unbox<ExactCapacityFenceReference> d?capacityFence)
            | _ -> unexpected "capacity settlement" d?capacitySettlement

        let fatality =
            match unbox<string> d?fatality with
            | "NoFatality" -> FatalityDecision.NoFatality
            | "FatalAfterSettlement" -> FatalityDecision.FatalAfterSettlement
            | _ -> unexpected "fatality" d?fatality

        { Resolution = resolution
          Breaker = breaker
          CapacitySettlement = capacity
          Fatality = fatality }

    let private run failure lifecycle (key: ChatExecutionKey) capacity (provider: ProviderRecoveryFacts option) =
        createObj
            [ "failure" ==> failureView failure
              "phase" ==> phaseLabel lifecycle
              "executionKey" ==> box key
              "capacityFence" ==> fenceView capacity
              "provider" ==> (match provider with Some facts -> providerView facts | None -> null) ]
        |> library.decide
        |> decisionOf key provider

    let decideSupersession key lifecycle capacity : ExecutionFailureDecision =
        run ExecutionFailure.Superseded lifecycle key capacity None

    let decide (input: ExecutionFailureInput) : ExecutionFailureDecision =
        run input.Failure input.Lifecycle input.ExecutionKey input.Capacity (Some input.Provider)
