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

    // Fable Tasks do not expose IsCompleted; this bit belongs to the
    // single in-flight completion cell and is not a second blogger state.
    let mutable inFlightTask: Task<unit> option = None
    let mutable inFlightCompleted = true

    let startAsTask (work: Async<unit>) : Task<unit> =
        let completion = TaskCompletionSource<unit>()
        inFlightCompleted <- false

        Async.StartImmediate(
            async {
                try
                    do! work
                finally
                    inFlightCompleted <- true
                    completion.SetResult(())
            }
        )

        completion.Task

    let isBusyUnlocked () =
        match inFlightTask with
        | Some _ when not inFlightCompleted -> true
        | _ -> false

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

    member private _.DisarmRecoverySlot() : unit =
        lock lockObj (fun () -> slotArmed <- false)

    member this.Snapshot: CompanionMemory = this.Memory
    member this.GetMemory() : CompanionMemory = this.Memory
    member _.IsBusy: bool = lock lockObj isBusyUnlocked
    member _.InFlightTask: Task<unit> option = lock lockObj (fun () -> inFlightTask)

    member this.WaitInFlightAsync() : Task =
        let tOpt = lock lockObj (fun () -> inFlightTask)

        match tOpt with
        | Some t -> t :> Task
        | None -> Task.FromResult(()) :> Task

    /// CTX-006: one armed slot may prepend a squash sub-request to the main request.
    ///
    /// All three `RecoverySlot.mayRecover` conditions are checked here, inside the
    /// single flight: the arming flag (this sequence saw a real failure), the odd
    /// fallback Offset, and at least one frame to squash. The decision never reaches
    /// the transform boundary (CTX-002).
    member this.Submit
        (
            currentProjection: ProviderSemanticProjection,
            blogFn: ProviderSemanticProjection -> BloggerDeltaChunk -> Task<Result<PromptKey, string>>,
            ?squashFn: int -> Task<Result<BlogProjectionState, string>>,
            ?cursorOffset: unit -> byte
        ) : CompanionOutcome =
        lock lockObj (fun () ->
            if isBusyUnlocked () then
                SkippedBusy
            else
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
                        // ENFORCER-047: the companion sends the prompt and returns.
                        // The commit happens in the Blogger continuation transform
                        // (EnforcerHost.handleContinuation → commitCycle →
                        // BlogEntryCommitted), not here. No persistSuccessful, no
                        // terminal wait.
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

                        inFlightTask <- Some t
                        Submitted)
