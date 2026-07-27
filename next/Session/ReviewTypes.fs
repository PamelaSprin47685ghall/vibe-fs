namespace Wanxiangshu.Next.Session

/// Port used by ReviewerHost / HostReviewGuard to read the current tree hash.
/// Distinct from OpenCode.GitTreePort adapters that wrap this shape.
[<Struct>]
type GitTreePort = { GetTreeHash: unit -> string }

[<RequireQualifiedAccess>]
type ReviewVerdict =
    | Perfect
    | Revise

/// Finish decision for a durable review barrier (journal-backed).
[<RequireQualifiedAccess>]
type ReviewFinishResult =
    | Confirmed
    | NeedsReview
