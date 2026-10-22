namespace Wanxiangshu.Repository.Knowledge.Casebook

/// CASE-004: replay stored observations against the current worktree —
/// read-only, never writing the subject. FileRead re-reads and re-hashes;
/// GlobResult re-enumerates; GrepResult re-searches. Any missing/changed
/// result makes the whole replay Stale (freshness hint, not proof).
module CasebookReplay =

    /// Replay the whole stored observation set. Missing any single
    /// observation (deleted file, unreadable) → Stale.
    val replayAll: root: string -> stored: Observation list -> System.Threading.Tasks.Task<Observation list>
