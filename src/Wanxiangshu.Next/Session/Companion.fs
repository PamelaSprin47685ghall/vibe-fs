namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection

/// Companion state wrapper with a single mutable in-flight Task gate.
type Companion(?initialMemory: CompanionMemory, ?durable: ICompanionDurablePort, ?sessionId: SessionId) =
    let lockObj = obj ()

    /// CTX-006 / FALLBACK-012: whether this slot was reached by a real failure.
    ///
    /// Control-flow state only — never journalled, never derived from the cursor's
    /// parity. A restart leaves it false (`RecoverySlot.afterRestart`), which is the
    /// safe side: the worst case is one missed compression opportunity.
    let mutable slotArmed = false

    let restoredMemory =
        match initialMemory with
        | Some memory -> Some memory
        | None ->
            match durable, sessionId with
            | Some port, Some sid ->
                match port.Load sid with
                | Ok memory -> memory
                | Error error -> raise (InvalidOperationException error)
            | _ -> None

    let mutable blogProjection: BlogProjectionState =
        restoredMemory
        |> Option.map (fun m -> m.Blog)
        |> Option.defaultValue BlogProjection.empty

    let mutable xTraceProjection: XTraceProjectionState =
        restoredMemory
        |> Option.map (fun m -> m.XTrace)
        |> Option.defaultValue XTraceProjection.empty

    let mutable latestB: BlogText option =
        restoredMemory |> Option.bind (fun m -> m.EffectiveFrames)

    /// COMPANION-002: the companion Blogger Session Y for this X.
    ///
    /// A runtime mirror of the durable link, not a second writer: `CompanionHost`
    /// owns the create/abort decision and writes the fact through
    /// `ICompanionDurablePort`, then tells this cache. Restored on construction so a
    /// restart reuses the same Y instead of creating a second one.
    let mutable bloggerSessionId: SessionId option =
        restoredMemory |> Option.bind (fun m -> m.BloggerSessionId)

    // Physical send Task is not the Blogger busy authority (ENFORCER-047).
    // Busy = BloggerRuntimeState.InFlight on the coordinator cell.
    // Keep a fire-and-forget handle only for WaitInFlightAsync diagnostics.
    let mutable lastSendTask: Task<unit> option = None

    let startAsTask (work: Async<unit>) : Task<unit> =
        let completion = TaskCompletionSource<unit>()

        Async.StartImmediate(
            async {
                try
                    do! work
                finally
                    completion.SetResult(())
            }
        )

        completion.Task

    member _.Memory: CompanionMemory =
        lock lockObj (fun () ->
            { Blog = blogProjection
              EffectiveFrames = latestB
              BloggerSessionId = bloggerSessionId
              XTrace = xTraceProjection })

    /// Mirror a durable Blogger link that `CompanionHost` already recorded.
    member _.RecordBloggerLinked(bloggerId: SessionId) : unit =
        lock lockObj (fun () -> bloggerSessionId <- Some bloggerId)

    /// Mirror a durable Blogger unlink. `None` again, so the next transform creates
    /// a fresh Y rather than prompting an aborted session forever.
    member _.RecordBloggerClosed() : unit =
        lock lockObj (fun () -> bloggerSessionId <- None)

    /// COMPANION-007: refresh the in-memory XTrace mirror after the transform
    /// boundary captured new parts. Without this the chunker keeps mapping the
    /// ingest cursor against the trace captured at construction, which is empty
    /// at first and re-reads the projection head every round.
    member _.RefreshXTrace(state: XTraceProjectionState) : unit =
        lock lockObj (fun () -> xTraceProjection <- state)

    /// FALLBACK-012 / CTX-006: arm the recovery slot after a real failure.
    ///
    /// One writer, one scope: this is the Y half of `PluginRuntimeScope.ArmRecovery`.
    /// The Companion's single-flight gate guarantees the slot sequence is linear, so
    /// arming cannot race a concurrent squash decision.
    member _.ArmRecoverySlot() : unit =
        lock lockObj (fun () -> slotArmed <- true)

    member _.DisarmRecoverySlot() : unit =
        lock lockObj (fun () -> slotArmed <- false)

    member _.IsRecoveryArmed: bool = lock lockObj (fun () -> slotArmed)

    member this.Snapshot: CompanionMemory = this.Memory
    member this.GetMemory() : CompanionMemory = this.Memory

    /// Diagnostic only — not the busy definition (see BloggerRuntimeState).
    member _.LastSendTask: Task<unit> option = lock lockObj (fun () -> lastSendTask)

    member this.WaitInFlightAsync() : Task =
        let tOpt = lock lockObj (fun () -> lastSendTask)

        match tOpt with
        | Some t -> t :> Task
        | None -> Task.FromResult(()) :> Task

    /// Test/legacy Submit. Production single-flight is BloggerCoordinator.
    /// This path does NOT gate on a Task-busy flag.
    member this.Submit
        (
            currentProjection: ProviderSemanticProjection,
            blogFn: ProviderSemanticProjection -> BloggerDeltaChunk -> Task<Result<PromptKey, string>>,
            ?cursorOffset: unit -> byte
        ) : CompanionOutcome =
        lock lockObj (fun () ->
            let ingestCursor =
                XTraceProjection.semanticCursorFor blogProjection.Coverage.IngestedThroughSequence xTraceProjection

            match
                BloggerDelta.nextChunk
                    BloggerDelta.DeltaLimitBytes
                    ingestCursor
                    blogProjection.Coverage.CoverableTurnCutoffExclusive
                    currentProjection.Messages
            with
            | None -> Submitted
            | Some delta ->
                match durable, sessionId with
                | None, _
                | _, None -> DurableJournalUnavailable
                | Some _, Some _ ->
                    let t =
                        async {
                            try
                                let! sent = blogFn currentProjection delta |> Async.AwaitTask

                                match sent with
                                | Ok _ -> ()
                                | Error _ -> ()
                            with _ ->
                                ()
                        }
                        |> startAsTask

                    lastSendTask <- Some t
                    Submitted)
