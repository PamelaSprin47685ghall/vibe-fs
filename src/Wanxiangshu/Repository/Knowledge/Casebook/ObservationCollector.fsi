namespace Wanxiangshu.Repository.Knowledge.Casebook

/// CASE-003 / KR-003: per-session observation and substantive access collector.
type CasebookObservationCollector =
    new: unit -> CasebookObservationCollector

    /// Record one tool execution's observation for a session.
    member Collect: sessionId: string * toolName: string * args: obj * output: string -> unit

    /// Record one explicit file read observation for a session.
    member RecordFileRead: sessionId: string * path: string * output: string -> unit

    /// Observations collected so far for a session (normalized).
    member Drain: sessionId: string -> Observation list

    member Count: sessionId: string -> int
