namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Context.Trace

[<RequireQualifiedAccess>]
module DelegationHandoff =
    val childRange: startInclusive: XTraceCursor -> endExclusive: XTraceCursor -> XTraceRange
