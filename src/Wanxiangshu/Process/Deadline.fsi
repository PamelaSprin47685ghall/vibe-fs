namespace Wanxiangshu.Process

open System

type Deadline = private Deadline of expiresAt: DateTimeOffset

module Deadline =
    val MaxTimerWaitMs: int
    val ofBudget: now: DateTimeOffset -> budget: TimeSpan -> Deadline
    val remaining: clock: (unit -> DateTimeOffset) -> deadline: Deadline -> TimeSpan
    val isExpired: clock: (unit -> DateTimeOffset) -> deadline: Deadline -> bool
    val nextWaitMs: clock: (unit -> DateTimeOffset) -> deadline: Deadline -> int
