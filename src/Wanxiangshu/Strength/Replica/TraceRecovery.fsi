namespace Wanxiangshu.Strength.Replica

open Wanxiangshu.Strength
open Wanxiangshu.Strength.Projection

type StrengthTraceObservedPart =
    { CursorSequence: int64
      Kind: string
      ToolName: string option
      Body: string }

[<RequireQualifiedAccess>]
module StrengthTraceRecovery =
    val expectedParts: bundle: StrengthFrameBundle -> (string * string option * string) list

    val recoverRange:
        bundle: StrengthFrameBundle ->
        observed: StrengthTraceObservedPart list ->
            Result<StrengthTraceRange option, string>
