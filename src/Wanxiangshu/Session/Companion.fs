namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel
open Wanxiangshu.Journal
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection

/// Companion state wrapper with a single mutable in-flight Task gate.
type Companion(?initialMemory: CompanionMemory, ?durable: ICompanionDurablePort, ?sessionId: SessionId) =
    let lockObj = obj ()

    /// CTX-006 / FALLBACK-012: one-shot recovery material waiter.
    ///
    /// Physical possession only — presence means a recovery Task is waiting for
    /// the next main material. Not a business stage: failure registers the waiter
    /// via `StartRecoveryOpportunity`; material offers complete it once via
    /// `OfferRecoveryMaterial`. A restart leaves `None` (safe: at most one missed
    /// compression opportunity, matching `RecoverySlot.afterRestart`).
    // DSL-MUTABLE: resource — one-shot recovery material waiter
    let mutable recoveryWaiter: TaskCompletionSource<unit> option = None

    let restoredMemory = initialMemory

    // DSL-MUTABLE: resource — in-memory blog projection mirror (durable-backed)
    let mutable blogProjection: BlogProjectionState =
        restoredMemory
        |> Option.map (fun m -> m.Blog)
        |> Option.defaultValue BlogProjection.empty

    // DSL-MUTABLE: resource — in-memory X-trace projection mirror (durable-backed)
    let mutable xTraceProjection: XTraceProjectionState =
        restoredMemory
        |> Option.map (fun m -> m.XTrace)
        |> Option.defaultValue XTraceProjection.empty

    // DSL-MUTABLE: resource — last effective blog frames cache
    let mutable latestB: BlogText option =
        restoredMemory |> Option.bind (fun m -> m.EffectiveFrames)

    /// COMPANION-002: the companion Blogger Session Y for this X.
    ///
    /// A runtime mirror of the durable link, not a second writer: `CompanionHost`
    /// owns the create/abort decision and writes the fact through
    /// `ICompanionDurablePort`, then tells this cache. Restored on construction so a
    /// restart reuses the same Y instead of creating a second one.
    // DSL-MUTABLE: resource — cached companion Blogger session id
    let mutable bloggerSessionId: SessionId option =
        restoredMemory |> Option.bind (fun m -> m.BloggerSessionId)

    // Physical send Task is not the Blogger busy authority (ENFORCER-047).
    // Busy = host flight ownership (HasFlight) only.
    // Keep a fire-and-forget handle only for WaitInFlightAsync diagnostics.
    // DSL-MUTABLE: single-flight — fire-and-forget send task handle
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

    /// FALLBACK-012 / CTX-006: open a one-shot recovery opportunity after a real failure.
    ///
    /// Registers a physical waiter Task. Opportunity exists while the Task is unfinished.
    /// A second call while the first waiter is unconsumed reuses it (one opportunity).
    member _.StartRecoveryOpportunity() : Task =
        let waiter =
            lock lockObj (fun () ->
                match recoveryWaiter with
                | Some existing -> existing
                | None ->
                    let created =
                        TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                    recoveryWaiter <- Some created
                    created)

        waiter.Task :> Task

    /// Material boundary: complete the pending recovery waiter if any.
    /// Returns true when a waiter was taken (recovery path may consume this material).
    /// Second offer with no registered waiter is a no-op.
    member _.OfferRecoveryMaterial() : bool =
        let waiter =
            lock lockObj (fun () ->
                let current = recoveryWaiter
                recoveryWaiter <- None
                current)

        match waiter with
        | Some tcs ->
            AsyncSupport.trySetResult tcs () |> ignore
            true
        | None -> false

    member this.Snapshot: CompanionMemory = this.Memory
    member this.GetMemory() : CompanionMemory = this.Memory

    /// Diagnostic only — not the busy definition (busy = host HasFlight).
    member _.LastSendTask: Task<unit> option = lock lockObj (fun () -> lastSendTask)

    member this.WaitInFlightAsync() : Task =
        let tOpt = lock lockObj (fun () -> lastSendTask)

        match tOpt with
        | Some t -> t :> Task
        | None -> Task.FromResult(()) :> Task
