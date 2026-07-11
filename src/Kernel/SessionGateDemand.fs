module Wanxiangshu.Kernel.SessionGateDemand

/// Which subagent settlement gate is open (priority: Fallback → Todo → Review).
[<RequireQualifiedAccess>]
type SessionGateDemand =
    | FallbackContinue
    | TodoNudge
    | ReviewNudge
    | Settled

let resolveGateDemand (needFallbackContinue: bool) (needTodoNudge: bool) (needReviewNudge: bool) : SessionGateDemand =
    if needFallbackContinue then
        SessionGateDemand.FallbackContinue
    elif needTodoNudge then
        SessionGateDemand.TodoNudge
    elif needReviewNudge then
        SessionGateDemand.ReviewNudge
    else
        SessionGateDemand.Settled
