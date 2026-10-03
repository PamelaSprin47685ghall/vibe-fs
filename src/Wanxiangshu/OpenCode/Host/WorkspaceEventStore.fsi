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
    /// durable-events-019 registration-necessity seam: the production program
    /// with exactly one named registration removed, all others kept in order.
    val programWithoutRegistration: ruleName: string -> IntegrationRule list
