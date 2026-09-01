namespace Wanxiangshu.Execution.Failure

[<RequireQualifiedAccess>]
module ExecutionFailurePolicy =
    val decide: input: ExecutionFailureInput -> ExecutionFailureDecision
