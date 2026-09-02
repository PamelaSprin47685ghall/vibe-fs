namespace Wanxiangshu.Enforcer

open Wanxiangshu.Repository.Knowledge.Casebook

/// CASE-003: per-session observation collector, fed by the Host
/// tool.execute.after boundary (args + rendered output — never transcript
/// text). Capture is best-effort: unparseable executions are skipped; the
/// buffer is drained into an archive when the Inspector session terminates
/// (the caller decides when — collector never decides lifecycle).
type ObservationCollector =
    new: unit -> ObservationCollector

    /// Record one tool execution's observation for a session.
    member Collect: sessionId: string * toolName: string * args: obj * output: string -> unit

    /// Observations collected so far for a session (normalized).
    member Drain: sessionId: string -> Observation list

    member Count: sessionId: string -> int
