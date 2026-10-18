namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Context.Trace

[<RequireQualifiedAccess>]
module HandoffSurface =

    let private rangeView (range: XTraceRange) =
        box
            {| start = range |> XTraceRange.startInclusive |> XTraceCursor.sequence |> int
               ``end`` = range |> XTraceRange.endExclusive |> XTraceCursor.sequence |> int |}

    let childRange (startInclusive: int) (endExclusive: int) : obj =
        DelegationHandoff.childRange
            (startInclusive |> int64 |> XTraceCursor.create)
            (endExclusive |> int64 |> XTraceCursor.create)
        |> rangeView
