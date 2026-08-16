namespace Wanxiangshu.Process

open System

type Deadline = private Deadline of expiresAt: DateTimeOffset

module Deadline =

    /// JS/Int32 ceiling for setTimeout: a larger delay is clamped/rejected by the
    /// runtime, so any wait longer than this must be segmented. 0x7FFFFFFF ms
    /// (~24.8 days).
    ///
    /// Plain `let`, not `[<Literal>]`: Fable inlines a literal and emits no export,
    /// so a layer 1 test could not read the bound it must assert against.
    let MaxTimerWaitMs = 2_147_483_647

    /// Build a deadline from the current clock and a time budget, clamping to
    /// DateTimeOffset.MaxValue so the calculation cannot overflow.
    let ofBudget (now: DateTimeOffset) (budget: TimeSpan) : Deadline =
        let safeBudget =
            try
                let maxBudget = DateTimeOffset.MaxValue - now
                if budget > maxBudget then maxBudget else budget
            with _ ->
                budget

        Deadline(now.Add(safeBudget))

    let remaining (clock: unit -> DateTimeOffset) (Deadline expiresAt: Deadline) : TimeSpan =
        let rem = expiresAt - clock ()
        if rem < TimeSpan.Zero then TimeSpan.Zero else rem

    let isExpired (clock: unit -> DateTimeOffset) (Deadline expiresAt: Deadline) : bool = clock () >= expiresAt

    /// Next wait duration in milliseconds against the absolute deadline, capped at
    /// the JS timer ceiling. A huge legal estimate (tens of days) therefore returns
    /// the cap instead of overflowing int, so the caller can wait in segments.
    let nextWaitMs (clock: unit -> DateTimeOffset) (Deadline expiresAt: Deadline) : int =
        let rem = expiresAt - clock ()

        if rem <= TimeSpan.Zero then
            0
        else
            let total = int64 rem.TotalMilliseconds

            if total > int64 MaxTimerWaitMs then
                MaxTimerWaitMs
            else
                int total
