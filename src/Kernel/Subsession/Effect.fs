namespace Wanxiangshu.Kernel.Subsession.Types

type Effect =
    | DispatchPrompt of TurnPlan
    | QueryDispatchStatus of SessionId * TurnId
    | QuerySessionQuiescence of SessionId * TurnId
    | ClosePhysicalSession of SessionId
    | AbortHostSession of SessionId * TurnId
    | CancelPendingDispatch of TurnId
    | CompleteCaller of RunId * RunResult
    | RejectStart of StartRunError
    | DisposeActor
