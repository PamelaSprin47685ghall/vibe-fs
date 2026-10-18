namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Context.Trace

[<RequireQualifiedAccess>]
module DelegationHandoff =

    let childRange (startInclusive: XTraceCursor) (endExclusive: XTraceCursor) : XTraceRange =
        if XTraceCursor.isAfter startInclusive endExclusive then
            invalidArg "endExclusive" "delegation child range cannot retreat"

        XTraceRange.create startInclusive endExclusive
