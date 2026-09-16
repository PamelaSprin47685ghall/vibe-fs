namespace Wanxiangshu.Repository.Knowledge.Casebook

/// CASE-003 / KR-003: per-session observation and substantive access collector.
type CasebookObservationCollector =
    new: unit -> CasebookObservationCollector

    /// Record one tool execution's observation for a session.
    member Collect: sessionId: string * toolName: string * args: obj * output: string -> unit

    /// Record substantive access directly.
    member RecordSubstantive: sessionId: string * toolName: string * args: obj * committed: bool -> unit

    /// Observations collected so far for a session (normalized).
    member Drain: sessionId: string -> Observation list

    /// Substantive access paths collected so far for a session.
    member DrainPaths: sessionId: string -> string list

    /// AccessTracker collected so far for a session.
    member DrainTracker: sessionId: string -> AccessTracker

    member Count: sessionId: string -> int
