namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Collections.Generic

/// CASE-003 / KR-003: per-session observation and substantive access collector.
type CasebookObservationCollector() =

    let buffers = Dictionary<string, ResizeArray<Observation>>()
    let trackers = Dictionary<string, AccessTracker>()

    let appendObservation sessionId observation =
        match buffers.TryGetValue sessionId with
        | true, buffer -> buffer.Add observation
        | false, _ ->
            let buffer = ResizeArray<Observation>()
            buffer.Add observation
            buffers.[sessionId] <- buffer

    let getTracker sessionId =
        match trackers.TryGetValue sessionId with
        | true, tracker -> tracker
        | false, _ ->
            let tracker = CasebookCapture.createAccessTracker ()
            trackers.[sessionId] <- tracker
            tracker

    member _.Collect(sessionId: string, toolName: string, args: obj, output: string) : unit =
        let tracker = getTracker sessionId
        CasebookCapture.recordSubstantiveAccess tracker toolName args true
        match CasebookCapture.capture toolName args output with
        | None -> ()
        | Some observation -> appendObservation sessionId observation

    member _.RecordSubstantive(sessionId: string, toolName: string, args: obj, committed: bool) : unit =
        let tracker = getTracker sessionId
        CasebookCapture.recordSubstantiveAccess tracker toolName args committed

    member _.Drain(sessionId: string) : Observation list =
        match buffers.TryGetValue sessionId with
        | true, buffer ->
            let snapshot = buffer |> Seq.toList |> Observations.normalize
            buffers.Remove sessionId |> ignore
            snapshot
        | false, _ -> []

    member _.DrainPaths(sessionId: string) : string list =
        match trackers.TryGetValue sessionId with
        | true, tracker ->
            let paths = tracker.GetRelatedPaths()
            trackers.Remove sessionId |> ignore
            paths
        | false, _ -> []

    member _.DrainTracker(sessionId: string) : AccessTracker =
        match trackers.TryGetValue sessionId with
        | true, tracker ->
            trackers.Remove sessionId |> ignore
            tracker
        | false, _ -> CasebookCapture.createAccessTracker ()

    member _.Count(sessionId: string) : int =
        match buffers.TryGetValue sessionId with
        | true, buffer -> buffer.Count
        | false, _ -> 0
