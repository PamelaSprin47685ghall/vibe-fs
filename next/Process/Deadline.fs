namespace Wanxiangshu.Next.Process

open System

type Deadline = private Deadline of expiresAt: DateTimeOffset

module Deadline =

    let ofBudget (now: DateTimeOffset) (budget: TimeSpan) : Deadline = Deadline(now.Add(budget))

    let remaining (clock: unit -> DateTimeOffset) (Deadline expiresAt: Deadline) : TimeSpan =
        let rem = expiresAt - clock ()
        if rem < TimeSpan.Zero then TimeSpan.Zero else rem

    let isExpired (clock: unit -> DateTimeOffset) (Deadline expiresAt: Deadline) : bool = clock () >= expiresAt
