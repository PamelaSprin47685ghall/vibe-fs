namespace Wanxiangshu.Mission.Planning

/// Adapter over the wanxiangli lifecycle kernel for Plan-side review and
/// deliver. Reads only the typed InspectedWork projection, so Plan code keeps
/// its dependency on the kernel's projected fields explicit.
module PlanKernelSeam =
    /// One keyed obligation: the key names the finding, the value carries the
    /// work plan.
    type Obligation = { Key: string; Value: string }

    /// The active tenure's obligation ledger (key -> value, item-by-item).
    val activeObligations: gitCommonDir: string -> workId: string -> Obligation array

    /// True when the active tenure's obligation ledger is empty.
    val obligationsEmpty: gitCommonDir: string -> workId: string -> bool

    /// True when the named incumbency has finished its first review
    /// (reviewFinishedOnly: draft writes unlock only after this is true).
    val isReviewFinished: gitCommonDir: string -> workId: string -> incumbencyId: string -> bool

    /// True when the named incumbency's first review was empty (eligible for
    /// promotion; promotion itself is performed by deliver).
    val isEligible: gitCommonDir: string -> workId: string -> incumbencyId: string -> bool
