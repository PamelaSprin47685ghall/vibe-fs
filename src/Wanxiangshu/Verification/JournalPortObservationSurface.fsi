namespace Wanxiangshu.Verification

open System.Threading.Tasks

/// Port observation-timing proofs: each scenario drives a real AgentJournal
/// over a real filesystem EventStore and observes through the production
/// AgentJournalPortAdapter members — never a copy of the adapter or fold.
[<RequireQualifiedAccess>]
module JournalPortObservationSurface =

    /// Call-time reads: a port built before an append observes the new state on
    /// the very next member call; a port built after it observes the same.
    val liveReadScenario: commonDir: string -> writerTag: string -> Task<obj>

    /// One CommitHandle must move every related view together: while the
    /// physical append is parked mid-commit every member still reads the
    /// pre-commit state, and after release all of them read post-commit.
    val sameCommitViewScenario: commonDir: string -> writerTag: string -> Task<obj>

    /// check-then-subscribe: a waiter registered at an old revision wakes on the
    /// next committed fact with no polling or sleep in the observed direction.
    val revisionWaitScenario: commonDir: string -> writerTag: string -> Task<obj>

    /// A cancelled waiter unregisters and resolves to None without consuming a
    /// later committed change.
    val cancelWaiterScenario: commonDir: string -> writerTag: string -> Task<obj>

    /// Physical uncertainty must report unknown (never confirmed) and poison the
    /// writer once; the port must observe the poisoned latch, and later appends
    /// are known-not-attempted instead of silently succeeding.
    val poisonedUnknownAppendScenario: commonDir: string -> writerTag: string -> Task<obj>
