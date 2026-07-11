module Wanxiangshu.Kernel.SessionGateDemand

/// Which subagent settlement gate is open (priority: Fallback → Todo → Review).
[<RequireQualifiedAccess>]
type SessionGateDemand =
    | FallbackContinue
    | TodoNudge
    | ReviewNudge
    | Settled

[<RequireQualifiedAccess>]
type GateSignal =
    | FallbackContinue
    | TodoNudge
    | ReviewNudge

let resolveFromSignals (signals: GateSignal list) : SessionGateDemand =
    match signals |> List.tryHead with
    | Some GateSignal.FallbackContinue -> SessionGateDemand.FallbackContinue
    | Some GateSignal.TodoNudge -> SessionGateDemand.TodoNudge
    | Some GateSignal.ReviewNudge -> SessionGateDemand.ReviewNudge
    | None -> SessionGateDemand.Settled
