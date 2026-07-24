namespace Wanxiangshu.Next.Session

type ModelSide =
    | A
    | B

type FallbackState = { Side: ModelSide; Failures: int }

[<RequireQualifiedAccess>]
type FallbackDecision =
    | NextAttempt of FallbackState
    | Dead

module Fallback =

    let initial: FallbackState = { Side = ModelSide.A; Failures = 0 }

    let nextAttempt (state: FallbackState) : FallbackDecision =
        match state.Side with
        | ModelSide.A ->
            if state.Failures < 2 then
                FallbackDecision.NextAttempt
                    { Side = ModelSide.A
                      Failures = state.Failures + 1 }
            else
                FallbackDecision.NextAttempt
                    { Side = ModelSide.B
                      Failures = state.Failures + 1 }
        | ModelSide.B ->
            if state.Failures < 4 then
                FallbackDecision.NextAttempt
                    { Side = ModelSide.B
                      Failures = state.Failures + 1 }
            else
                FallbackDecision.Dead
