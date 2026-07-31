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

    let mutable latestB: BlogText option =
        restoredMemory |> Option.bind (fun m -> m.LatestB)

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

    let persistSuccessful (completion: BloggerCompletion) =
        match durable, sessionId with
        | Some port, Some sid ->
            match port.AppendSuccessful(sid, completion) with
            | Ok updated -> lock lockObj (fun () -> blogProjection <- updated)
            | Error error -> raise (InvalidOperationException error)
        | _ -> raise (InvalidOperationException "No durable Companion port")

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
              LatestB = latestB
              BloggerSessionId = bloggerSessionId })

    /// Mirror a durable Blogger link that `CompanionHost` already recorded.
    member _.RecordBloggerLinked(bloggerId: SessionId) : unit =
        lock lockObj (fun () -> bloggerSessionId <- Some bloggerId)

    /// Mirror a durable Blogger unlink. `None` again, so the next transform creates
    /// a fresh Y rather than prompting an aborted session forever.
    member _.RecordBloggerClosed() : unit =
        lock lockObj (fun () -> bloggerSessionId <- None)

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
            blogFn: ProviderSemanticProjection -> BloggerDeltaChunk -> Task<BloggerCompletion>,
            ?squashFn: int -> Task<Result<BlogProjectionState, string>>,
            ?cursorOffset: unit -> byte
        ) : CompanionOutcome =
        lock lockObj (fun () ->
            if isBusyUnlocked () then
                SkippedBusy
            else
                match
                    BloggerDelta.nextChunk
                        BloggerDelta.DeltaLimitBytes
                        blogProjection.Coverage.IngestCursor
                        blogProjection.Coverage.CoverableTurnCutoffExclusive
                        currentProjection.Messages
                with
                | None -> Submitted
                | Some delta ->
                    match durable, sessionId with
                    | None, _
                    | _, None -> DurableJournalUnavailable
                    | Some _, Some _ ->
                        let previousCutoff = blogProjection.Coverage.CoverableTurnCutoffExclusive
                        let previousDigest = blogProjection.Coverage.CoveredPrefixDigest

                        // CTX-006: decide the slot BEFORE the flight starts so the same
                        // flight owns the whole slot sequence (squash, then main).
                        let arming =
                            if slotArmed then
                                RecoverySlot.afterFailureAdvance
                            else
                                RecoverySlot.beginSequence

                        let offset = defaultArg cursorOffset (fun () -> 0uy) ()
                        let hasMaterial = not (List.isEmpty blogProjection.Frames)
                        let maySquash = squashFn.IsSome && RecoverySlot.mayRecover arming offset hasMaterial

                        // The squash covers the oldest ceil(m/2) frames (design §13.1).
                        let squashFrameCount =
                            if maySquash then
                                (List.length blogProjection.Frames + 1) / 2
                            else
                                0

                        // FALLBACK-012: whatever this slot decides, the arming it
                        // consumed does not survive into a later slot — a new failure
                        // is the only thing that arms again.
                        slotArmed <- false

                        let squashPhase =
                            async {
                                match squashFn, maySquash with
                                | Some squash, true ->
                                    let! outcome = squash squashFrameCount |> Async.AwaitTask

                                    match outcome with
                                    | Ok updated ->
                                        // CTX-012: the committed squash frames become
                                        // the base for the same slot's main request.
                                        lock lockObj (fun () -> blogProjection <- updated)
                                        return true
                                    | Error _ ->
                                        // SquashFailed: the slot failed (cursor already
                                        // advanced by the squash writer). Skip the main
                                        // request of this slot (design §13.4).
                                        return false
                                | _ -> return true
                            }

                        let t =
                            async {
                                try
                                    let! proceedToMain = squashPhase

                                    if proceedToMain then
                                        let! produced = blogFn currentProjection delta |> Async.AwaitTask

                                        let completion =
                                            if delta.NextCoverableTurnCutoffExclusive = previousCutoff then
                                                { produced with
                                                    NextCoveredPrefixDigest = previousDigest }
                                            else
                                                produced

                                        persistSuccessful completion

                                        let nextB =
                                            match latestB with
                                            | None -> completion.Text
                                            | Some old -> old + "\n\n" + completion.Text

                                        lock lockObj (fun () -> latestB <- Some nextB)
                                with _ ->
                                    ()
                            }
                            |> startAsTask

                        inFlightTask <- Some t
                        Submitted)
