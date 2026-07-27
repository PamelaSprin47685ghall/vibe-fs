namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open Xunit
open Wanxiangshu.Next.Process

module DeadlineTests =

    [<Fact>]
    let ``nextWaitMs_caps_huge_remaining_at_MaxTimerWaitMs`` () =
        let start = DateTimeOffset.UtcNow
        let farFuture = start.AddDays(90.0)
        let deadline = Deadline.ofBudget start (farFuture - start)
        let clock = fun () -> start
        let ms = Deadline.nextWaitMs clock deadline
        Assert.True(ms > 0, "Expected a positive wait for a far-future deadline")
        Assert.True(ms <= Deadline.MaxTimerWaitMs, "Expected wait capped at MaxTimerWaitMs")

    [<Fact>]
    let ``nextWaitMs_returns_exact_remaining_ms_when_small`` () =
        let start = DateTimeOffset.UtcNow
        let deadline = Deadline.ofBudget start (TimeSpan.FromMilliseconds(1234.0))
        let clock = fun () -> start
        let ms = Deadline.nextWaitMs clock deadline
        Assert.Equal(1234, ms)

    [<Fact>]
    let ``nextWaitMs_returns_zero_when_expired`` () =
        let start = DateTimeOffset.UtcNow
        let deadline = Deadline.ofBudget start (TimeSpan.FromMilliseconds(500.0))
        let clock = fun () -> start.AddMilliseconds(1000.0)
        let ms = Deadline.nextWaitMs clock deadline
        Assert.Equal(0, ms)

    [<Fact>]
    let ``nextWaitMs_decreases_as_clock_advances_and_reaches_zero`` () =
        let start = DateTimeOffset.UtcNow
        let totalMs = 10000L
        let deadline = Deadline.ofBudget start (TimeSpan.FromMilliseconds(float totalMs))
        let mutable now = start
        let clock = fun () -> now

        let first = Deadline.nextWaitMs clock deadline
        Assert.Equal(int totalMs, first)

        let mutable prev = first
        let mutable reachedZero = false

        while not reachedZero do
            now <- now.AddMilliseconds(137.0)
            let ms = Deadline.nextWaitMs clock deadline

            if ms = 0 then
                Assert.True(prev > 0, "Expected to reach zero only after positive waits")
                reachedZero <- true
            else
                Assert.True(ms < prev, sprintf "Expected strictly decreasing wait, prev=%d ms=%d" prev ms)
                prev <- ms

        Assert.True(reachedZero, "Expected wait to eventually reach zero")
