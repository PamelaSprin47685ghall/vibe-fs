module Wanxiangshu.Tests.RetryDispatchGovernorTestSupport

open Fable.Core
open Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor
open Wanxiangshu.Runtime.ToolSequenceThrottle

type MockClock(initialTime: float) =
    let mutable current = initialTime
    member _.Advance(ms: float) = current <- current + ms
    member _.GetMonotonicTimeMs() = current

    interface IClock with
        member _.GetMonotonicTimeMs() = current

type MockSleeper(clock: MockClock) =
    let mutable totalSleptMs = 0.0
    let mutable sleepCount = 0
    member _.TotalSleptMs = totalSleptMs
    member _.SleepCount = sleepCount

    interface ISleeper with
        member _.Sleep(ms: int) =
            clock.Advance(float ms)
            totalSleptMs <- totalSleptMs + float ms
            sleepCount <- sleepCount + 1
            Promise.lift ()

let transportKey workspace provider model =
    ProviderModelTransportKey.Create(workspace, provider, model)

let makeGovernor (rateLimitMs: int64) (clock: MockClock) (sleeper: MockSleeper) =
    RetryDispatchGovernor(rateLimitMs = rateLimitMs, clock = clock, sleeper = sleeper)
