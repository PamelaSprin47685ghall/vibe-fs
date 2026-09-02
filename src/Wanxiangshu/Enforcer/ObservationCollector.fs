namespace Wanxiangshu.Enforcer

open System.Collections.Generic
open Wanxiangshu.Repository.Knowledge.Casebook

/// CASE-003: per-session observation collector, fed by the Host
/// tool.execute.after boundary (args + rendered output — never transcript
/// text). Capture is best-effort: unparseable executions are skipped; the
/// buffer is drained into an archive when the Inspector session terminates
/// (the caller decides when — collector never decides lifecycle).
type ObservationCollector() =

    // DSL-MUTABLE: resource — per-session observation buffer registry
    let buffers = Dictionary<string, ResizeArray<Observation>>()

    let appendObservation sessionId observation =
        match buffers.TryGetValue sessionId with
        | true, buffer -> buffer.Add observation
        | false, _ ->
            // DSL-MUTABLE: algorithm-scratch — new observation buffer for dictionary insert
            let buffer = ResizeArray<Observation>()
            buffer.Add observation
            buffers.[sessionId] <- buffer

    /// Record one tool execution's observation for a session.
    member _.Collect(sessionId: string, toolName: string, args: obj, output: string) : unit =
        match CasebookCapture.capture toolName args output with
        | None -> ()
        | Some observation -> appendObservation sessionId observation

    /// Observations collected so far for a session (normalized).
    member _.Drain(sessionId: string) : Observation list =
        match buffers.TryGetValue sessionId with
        | true, buffer ->
            let snapshot = buffer |> Seq.toList |> Observations.normalize
            buffers.Remove sessionId |> ignore
            snapshot
        | false, _ -> []

    member _.Count(sessionId: string) : int =
        match buffers.TryGetValue sessionId with
        | true, buffer -> buffer.Count
        | false, _ -> 0
