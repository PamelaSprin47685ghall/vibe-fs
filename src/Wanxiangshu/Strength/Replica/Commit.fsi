namespace Wanxiangshu.Strength.Replica

[<RequireQualifiedAccess>]
type StrengthAppendOutcome =
    | Committed
    | Rejected
    | CommitUnknown

[<RequireQualifiedAccess>]
type StrengthDurableEvidence =
    | Matches
    | Absent
    | Conflicts
    | Unknown

[<RequireQualifiedAccess>]
type StrengthCommitDecision =
    | Proceed
    | FallBackK0
    | RetryAppend
    | FailClosed

[<RequireQualifiedAccess>]
module StrengthCommit =
    val resolvePrepared:
        appendOutcome: StrengthAppendOutcome -> durableEvidence: StrengthDurableEvidence -> StrengthCommitDecision

    val resolvePromotion:
        appendOutcome: StrengthAppendOutcome -> durableEvidence: StrengthDurableEvidence -> StrengthCommitDecision
