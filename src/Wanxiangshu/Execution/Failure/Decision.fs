namespace Wanxiangshu.Execution.Failure

open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

type ExecutionFailureInput =
    { Failure: ExecutionFailure
      Lifecycle: DurableExecutionLifecycle
      ExecutionKey: ChatExecutionKey
      Capacity: CapacityOwnership
      Provider: ProviderRecoveryFacts }

[<RequireQualifiedAccess>]
type ExecutionFailureResolution =
    | PreserveCurrentFact
    | AwaitAcceptanceReconciliation of ChatExecutionKey
    | RetryFreshAttempt of ProviderRecoveryAuthorization
    | TerminalizeAcceptedPreProvider of ChatExecutionKey * ChatExecutionTerminalDisposition
    | TerminalizeProviderStarted of ChatExecutionKey * ChatExecutionTerminalDisposition

type ExecutionFailureDecision =
    { Resolution: ExecutionFailureResolution
      Breaker: BreakerDecision
      CapacitySettlement: CapacitySettlement
      Fatality: FatalityDecision }
