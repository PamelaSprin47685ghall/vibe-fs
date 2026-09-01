namespace Wanxiangshu.Repository.Knowledge.Casebook

/// CASE-004: replay stored observations against the current worktree —
/// read-only, never writing the subject. FileRead re-reads and re-hashes;
/// GlobResult re-enumerates; GrepResult re-searches. Any missing/changed
/// result makes the whole replay Stale (freshness hint, not proof).
module CasebookReplay =

    /// Replay one observation; None = the observation cannot be reproduced
    /// (missing file / unreadable) — that is a change signal.
    val replayOne: root: string -> observation: Observation -> Observation option

    /// Replay the whole stored observation set. Missing any single
    /// observation (deleted file, unreadable) → Stale.
    val replayAll: root: string -> stored: Observation list -> Observation list
