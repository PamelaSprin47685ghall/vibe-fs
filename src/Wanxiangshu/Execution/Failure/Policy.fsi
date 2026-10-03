namespace Wanxiangshu.Execution.Failure

open Wanxiangshu.Execution.Session.ChatExecution

[<RequireQualifiedAccess>]
module ExecutionFailurePolicy =
    val decideSupersession:
        key: ChatExecutionKey ->
        lifecycle: DurableExecutionLifecycle ->
        capacity: CapacityOwnership ->
            ExecutionFailureDecision

    val decide: input: ExecutionFailureInput -> ExecutionFailureDecision
