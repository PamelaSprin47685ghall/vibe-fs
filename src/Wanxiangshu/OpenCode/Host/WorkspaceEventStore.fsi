namespace Wanxiangshu.OpenCode

open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal

/// Process-local EventStore owners keyed by git common-dir.
/// One acquired entry owns exactly one WriterId.ndjson and one CanonicalIntegrator.
/// No GitRawStore is created on the runtime append/replay path.
module WorkspaceEventStore =
    val acquire: commonDir: string -> IEventStore
    val tryCurrent: commonDir: string -> IEventStore option
    val release: commonDir: string -> unit
    val bootPort: commonDir: string -> IJournalEventStoreBoot
    /// The unified lifecycle work for one session, read from the already-owned
    /// workspace store. Missing workspace, missing store, missing current,
    /// missing work, or a blank common dir all yield None.
    val tryLifecycleWork: workspace: string -> workId: string -> Wanxiangshu.Mission.Lifecycle.LifecycleWork option
    /// Directory-option variant of tryLifecycleWork. None workspace yields None.
    val tryLifecycleWorkOfDirectory:
        workspaceDirectory: string option -> workId: string -> Wanxiangshu.Mission.Lifecycle.LifecycleWork option
    /// The same read when the caller already holds the store and must commit
    /// through it. Missing current or missing work yields None.
    val tryLifecycleWorkOfStore:
        store: IEventStore -> workId: string -> Wanxiangshu.Mission.Lifecycle.LifecycleWork option
    /// Every unified lifecycle work folded into a held store. A missing
    /// current yields None; an empty fold yields Some empty.
    val tryLifecycleWorksOfStore:
        store: IEventStore -> Map<string, Wanxiangshu.Mission.Lifecycle.LifecycleWork> option
    /// durable-events-019 registration-necessity seam: the production program
    /// with exactly one named registration removed, all others kept in order.
    val programWithoutRegistration: ruleName: string -> IntegrationRule list
