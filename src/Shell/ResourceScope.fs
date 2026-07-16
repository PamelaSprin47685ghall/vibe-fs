module Wanxiangshu.Shell.ResourceScope

open Fable.Core
open Wanxiangshu.Kernel.ResourcePlan

// ── Timer handle tracking ──

type private TimerEntry =
    { Handle: int
      DeadlineAtMs: int64
      Callback: unit -> unit }

/// RAII-based resource scope manager.
/// Automatically arms and clears JS timers based on ResourceSpec diffs.
/// On dispose, all timers are cleared.
type ResourceScope(callback: ResourceId -> unit) =
    let mutable activeTimers: Map<string, TimerEntry> = Map.empty
    let disposed = ref false

    let timerKey (id: ResourceId) : string =
        match id with
        | TurnDeadlineId tid -> "turn:" + tid
        | AbortDeadlineId tid -> "abort:" + tid
        | ReconciliationDeadlineId tid -> "recon:" + tid

    let clearTimer (key: string) =
        match Map.tryFind key activeTimers with
        | Some entry ->
            JS.clearTimeout entry.Handle
            activeTimers <- Map.remove key activeTimers
        | None -> ()

    let armTimer (spec: ResourceSpec) =
        let id, deadlineAtMs =
            match spec with
            | TurnDeadline(id, d) -> id, d.DeadlineAtMs
            | AbortDeadline(id, d) -> id, d.DeadlineAtMs
            | ReconciliationDeadline(id, d) -> id, d.DeadlineAtMs

        let key = timerKey id

        // Compute remaining time (allow for clock skew with floor of 0)
        let nowMs = int64 (JS.Constructors.Date.now ())
        let remaining = max 0L (deadlineAtMs - nowMs)

        let handle =
            JS.setTimeout
                (fun () ->
                    if not disposed.Value then
                        callback id)
                (int (min remaining (int64 System.Int32.MaxValue)))

        activeTimers <-
            Map.add
                key
                { Handle = handle
                  DeadlineAtMs = deadlineAtMs
                  Callback = fun () -> callback id }
                activeTimers

    /// Reconcile a list of desired resource specs against currently active timers.
    /// Acquires new timers, releases stale ones, and re-arms timers whose
    /// deadline time has changed.
    member _.Reconcile(nextSpecs: ResourceSpec list) : unit =
        if disposed.Value then
            ()

        // Build current set of (key, deadlineAtMs) pairs
        let prevKeyDeadlines =
            activeTimers
            |> Map.toSeq
            |> Seq.map (fun (key, entry) -> (key, entry.DeadlineAtMs))
            |> Set.ofSeq

        let nextKeyDeadlines =
            nextSpecs
            |> List.map (fun spec ->
                let key =
                    match spec with
                    | TurnDeadline(id, _) -> timerKey id
                    | AbortDeadline(id, _) -> timerKey id
                    | ReconciliationDeadline(id, _) -> timerKey id

                let deadlineAtMs =
                    match spec with
                    | TurnDeadline(_, d)
                    | AbortDeadline(_, d)
                    | ReconciliationDeadline(_, d) -> d.DeadlineAtMs

                (key, deadlineAtMs))
            |> Set.ofList

        // Release stale or changed
        for (key, _) in Set.difference prevKeyDeadlines nextKeyDeadlines do
            clearTimer key

        // Also release entries whose deadline time changed
        for (key, deadlineAtMs) in nextKeyDeadlines do
            if Set.contains (key, deadlineAtMs) prevKeyDeadlines then
                // Unchanged — skip
                ()
            else if activeTimers |> Map.containsKey key then
                // Key exists but deadline changed — clear and re-arm below
                clearTimer key

        // Acquire new or re-arm changed
        for spec in nextSpecs do
            let key =
                match spec with
                | TurnDeadline(id, _) -> timerKey id
                | AbortDeadline(id, _) -> timerKey id
                | ReconciliationDeadline(id, _) -> timerKey id

            if not (Map.containsKey key activeTimers) then
                armTimer spec

    /// Clear all active timers (called on dispose).
    member _.ClearAll() : unit =
        disposed.Value <- true

        for KeyValue(_, entry) in activeTimers do
            JS.clearTimeout entry.Handle

        activeTimers <- Map.empty

    /// Get the count of active timers (for testing/assertion).
    member _.ActiveCount: int = activeTimers.Count
