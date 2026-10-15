namespace Wanxiangshu.Process

open System
open Fable.Core
open Fable.Core.JsInterop

/// 实现唯一来源：wanxiangli/time（CUT-21）。Deadline 保持不透明 DU，
/// 内部持有库的不透明 deadline 值；主仓签名与语义不变。
type Deadline = private Deadline of libraryValue: obj

module Deadline =

    // wanxiangli/time 的具名 Time 容器。
    [<Import("Time", "wanxiangli/time")>]
    let private library: obj = jsNative

    let private isoOf (value: DateTimeOffset) : string = value.ToString("O")

    // Plain `let`, not `[<Literal>]`: Fable inlines a literal and emits no export,
    // so a layer 1 test could not read the bound it must assert against.
    let MaxTimerWaitMs: int = unbox<int> library?maxTimerWaitMs

    /// Build a deadline from the current clock and a time budget, clamping to
    /// DateTimeOffset.MaxValue so the calculation cannot overflow.
    let ofBudget (now: DateTimeOffset) (budget: TimeSpan) : Deadline =
        Deadline(library?deadlineCreate(isoOf now, budget.TotalMilliseconds))

    let remaining (clock: unit -> DateTimeOffset) (Deadline value: Deadline) : TimeSpan =
        let milliseconds = library?deadlineRemainingMs(isoOf (clock ()), value)
        TimeSpan.FromMilliseconds(unbox<float> milliseconds)

    let isExpired (clock: unit -> DateTimeOffset) (Deadline value: Deadline) : bool =
        unbox<bool> (library?deadlineIsExpired(isoOf (clock ()), value))

    /// Next wait duration in milliseconds against the absolute deadline, capped at
    /// the JS timer ceiling. A huge legal estimate (tens of days) therefore returns
    /// the cap instead of overflowing int, so the caller can wait in segments.
    let nextWaitMs (clock: unit -> DateTimeOffset) (Deadline value: Deadline) : int =
        unbox<int> (library?deadlineNextWaitMs(isoOf (clock ()), value))
