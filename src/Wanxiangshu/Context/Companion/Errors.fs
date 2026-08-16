namespace Wanxiangshu.Context.Companion

/// Domain error / context values used by direct CE programs (not a Flow AST).
/// Moved out of Foundation/Flow.fs (rotation-2): these are
/// Context/Companion semantics, not universe-level primitives.

[<RequireQualifiedAccess>]
type CompanionError =
    | ProjectionFailed of string
    | BloggerFailed of string

type CompanionContext = { SessionId: string }
