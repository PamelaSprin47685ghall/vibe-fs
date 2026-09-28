namespace Wanxiangshu.OpenCode.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal

/// Restart drops the process-local execution bindings
/// (`SessionExecutionBinding`), while the road's parented children survive as
/// durable handle records. Without restoring that evidence a recovered road can
/// never dispatch to its fixed DevOps again: the managed prompt is refused with
/// `PROMPT-006: parented session has no frozen agent binding`
/// (crash-reconciliation-020, managed-session-lifecycle-024).
module SessionBindingRecovery =

    /// Rebind every durable parented child to the agent its handle was linked
    /// with. Only live handle lifecycles are restored; Abandoned and Retired
    /// tombstones stay tombstones.
    val restoreFromProjection: projections: AgentProjectionSet -> unit

    /// Rebind every durable parented child to the agent its handle was linked
    /// with. Only live handle lifecycles are restored; Abandoned and Retired
    /// tombstones stay tombstones.
    val restoreFromDurable: journal: AgentJournal -> unit
