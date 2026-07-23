namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Tools

type TranscriptEvent = { Cursor: int; Json: string }

type BlogCheckpoint = { Watermark: int; Content: string }

type CompanionOutcome =
    | Submitted
    | SkippedBusy

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Companion =

    /// Pure jsonDelta: returns only contiguous events after the last submitted cursor (watermark).
    let jsonDelta (previous: BlogCheckpoint option) (current: TranscriptEvent list) : TranscriptEvent list =
        let watermark =
            match previous with
            | Some cp -> cp.Watermark
            | None -> 0

        let filtered =
            current
            |> List.filter (fun e -> e.Cursor > watermark)
            |> List.sortBy (fun e -> e.Cursor)

        match filtered with
        | [] -> []
        | head :: _ ->
            let startCursor = if watermark > 0 then watermark + 1 else head.Cursor

            filtered
            |> List.fold
                (fun (acc, expected) e ->
                    if e.Cursor = expected then
                        (e :: acc, expected + 1)
                    else
                        (acc, expected))
                ([], startCursor)
            |> fst
            |> List.rev

    /// Helper for jsonDelta taking a BlogCheckpoint directly.
    let jsonDeltaCheckpoint (previous: BlogCheckpoint) (current: TranscriptEvent list) : TranscriptEvent list =
        jsonDelta (Some previous) current

    /// Helper for jsonDelta taking a watermark integer directly.
    let jsonDeltaWatermark (watermark: int) (current: TranscriptEvent list) : TranscriptEvent list =
        jsonDelta (Some { Watermark = watermark; Content = "" }) current

    /// Pure compressPrefix: delegates to MessageTransform.replacePrefix while preserving tail after watermark.
    let compressPrefix (messages: HostMessage list) (checkpoint: BlogCheckpoint option) : HostMessage list =
        match checkpoint with
        | None -> messages
        | Some cp -> MessageTransform.replacePrefix messages cp.Content (Index cp.Watermark)

    /// Helper for compressPrefix taking a BlogCheckpoint directly.
    let compressPrefixCheckpoint (messages: HostMessage list) (checkpoint: BlogCheckpoint) : HostMessage list =
        compressPrefix messages (Some checkpoint)

/// Companion state wrapper with a single mutable in-flight Task gate.
type Companion(?initialCheckpoint: BlogCheckpoint) =
    let lockObj = obj ()
    let mutable currentCheckpoint: BlogCheckpoint option = initialCheckpoint
    let mutable inFlightTask: Task<unit> option = None

    /// Returns current checkpoint snapshot.
    member _.Snapshot: BlogCheckpoint option = lock lockObj (fun () -> currentCheckpoint)

    /// Helper method for snapshot access.
    member this.GetSnapshot() : BlogCheckpoint option = this.Snapshot

    /// Returns true if an async blog operation is currently in-flight.
    member _.IsBusy: bool =
        lock lockObj (fun () ->
            match inFlightTask with
            | Some t -> not t.IsCompleted
            | None -> false)

    /// Current in-flight task, if any.
    member _.InFlightTask: Task<unit> option = lock lockObj (fun () -> inFlightTask)

    /// Awaits the current in-flight task if running.
    member this.WaitInFlightAsync() : Task =
        let tOpt = lock lockObj (fun () -> inFlightTask)

        match tOpt with
        | Some t -> t :> Task
        | None -> Task.CompletedTask

    /// Blogger context reset: updates checkpoint if idle, returns true; returns false if busy.
    member this.TryRebase(newCheckpoint: BlogCheckpoint option) : bool =
        lock lockObj (fun () ->
            match inFlightTask with
            | Some t when not t.IsCompleted -> false
            | _ ->
                currentCheckpoint <- newCheckpoint
                true)

    /// Helper for TryRebase taking BlogCheckpoint directly.
    member this.TryRebase(newCheckpoint: BlogCheckpoint) : bool = this.TryRebase(Some newCheckpoint)

    /// Submit: starts async blog function only when idle, returns SkippedBusy when busy, never queues.
    /// Completion updates checkpoint atomically; failure leaves main caller unaffected.
    member this.Submit(events: TranscriptEvent list, blogFn: TranscriptEvent list -> Async<string>) : CompanionOutcome =
        lock lockObj (fun () ->
            match inFlightTask with
            | Some t when not t.IsCompleted -> SkippedBusy
            | _ ->
                let targetWatermark =
                    if List.isEmpty events then
                        match currentCheckpoint with
                        | Some cp -> cp.Watermark
                        | None -> 0
                    else
                        events |> List.map (fun e -> e.Cursor) |> List.max

                let t =
                    Task.Run<unit>(fun () ->
                        async {
                            try
                                let! content = blogFn events

                                let cp =
                                    { Watermark = targetWatermark
                                      Content = content }

                                lock lockObj (fun () -> currentCheckpoint <- Some cp)
                            with _ ->
                                ()
                        }
                        |> Async.StartAsTask)

                inFlightTask <- Some t
                Submitted)

    member this.Submit(events: TranscriptEvent list, blogFn: TranscriptEvent list -> Task<string>) : CompanionOutcome =
        this.Submit(events, (fun evs -> blogFn evs |> Async.AwaitTask))

    member this.Submit
        (events: TranscriptEvent list, blogFn: TranscriptEvent list -> Async<BlogCheckpoint>)
        : CompanionOutcome =
        lock lockObj (fun () ->
            match inFlightTask with
            | Some t when not t.IsCompleted -> SkippedBusy
            | _ ->
                let t =
                    Task.Run<unit>(fun () ->
                        async {
                            try
                                let! cp = blogFn events
                                lock lockObj (fun () -> currentCheckpoint <- Some cp)
                            with _ ->
                                ()
                        }
                        |> Async.StartAsTask)

                inFlightTask <- Some t
                Submitted)

    member this.Submit
        (events: TranscriptEvent list, blogFn: TranscriptEvent list -> Task<BlogCheckpoint>)
        : CompanionOutcome =
        this.Submit(events, (fun evs -> blogFn evs |> Async.AwaitTask))

    member this.Submit(blogFn: unit -> Async<BlogCheckpoint>) : CompanionOutcome =
        lock lockObj (fun () ->
            match inFlightTask with
            | Some t when not t.IsCompleted -> SkippedBusy
            | _ ->
                let t =
                    Task.Run<unit>(fun () ->
                        async {
                            try
                                let! cp = blogFn ()
                                lock lockObj (fun () -> currentCheckpoint <- Some cp)
                            with _ ->
                                ()
                        }
                        |> Async.StartAsTask)

                inFlightTask <- Some t
                Submitted)

    member this.Submit(blogFn: unit -> Task<BlogCheckpoint>) : CompanionOutcome =
        this.Submit(fun () -> blogFn () |> Async.AwaitTask)
