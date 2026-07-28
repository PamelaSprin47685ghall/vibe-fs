namespace Wanxiangshu.Next.Session

/// Pure modulo-4 A/A/B/B cursor. No Dead state.
type ModelSide =
    | A
    | B

/// Offset is always 0..3. Advance on provider retry only.
type FallbackMemory = { Offset: byte }

[<RequireQualifiedAccess>]
type FallbackDecision = NextAttempt of FallbackMemory

module Fallback =

    let initial: FallbackMemory = { Offset = 0uy }

    let side (offset: byte) : ModelSide =
        match offset with
        | 0uy
        | 1uy -> ModelSide.A
        | 2uy
        | 3uy -> ModelSide.B
        | _ -> invalidOp "Fallback offset must be in range 0..3"

    let advance (offset: byte) : byte = byte ((int offset + 1) % 4)

    /// Provider retry advances cursor permanently in this Logical Run.
    /// Success does not call this and does not reset offset.
    let nextAttempt (state: FallbackMemory) : FallbackDecision =
        FallbackDecision.NextAttempt { Offset = advance state.Offset }

    let currentSide (state: FallbackMemory) : ModelSide = side state.Offset
