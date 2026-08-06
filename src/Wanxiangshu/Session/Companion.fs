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

/// A single recovery opportunity. Never re-armed: a real failure creates one,
/// and the next material either consumes it or a cancellation/loss disarms it.
///
/// A plain physical signal, not a waiter: the production consumer decides at
/// the material boundary (`maySquash` reads `IsRecoveryArmed`) and clears the
/// slot when a squash starts (`DisarmRecoverySlot`). A `TaskCompletionSource`
/// here would be a dead tail — nothing in production ever awaits it, and
/// cancelling an un-awaited promise rejects it into an unhandled rejection.
[<RequireQualifiedAccess>]
type RecoveryArming =
    | NotArmed
    | Armed

/// Companion state wrapper with a single mutable in-flight Task gate.
type Companion(?initialMemory: CompanionMemory, ?durable: ICompanionDurablePort, ?sessionId: SessionId) =
    let lockObj = obj ()

    /// CTX-006 / FALLBACK-012: whether this slot was reached by a real failure.
    ///
    /// One-shot physical waiter, not a control-flow boolean. A restart leaves it
    /// `NotArmed` (`RecoverySlot.afterRestart`), which is the safe side: the worst
    /// case is one missed compression opportunity.
    let mutable arming = RecoveryArming.NotArmed

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

    /// FALLBACK-012 / CTX-006: create a one-shot recovery slot after a real failure.
    ///
    /// One writer, one scope: this is the Y half of `PluginRuntimeScope.ArmRecovery`.
    /// The Companion's single-flight gate guarantees the slot sequence is linear, so
    /// arming cannot race a concurrent squash decision. An existing waiter is left
    /// untouched: a second failure while the first has not been consumed is not a
    /// second recovery opportunity (the slot already exists).
    member _.ArmRecoverySlot() : unit =
        lock lockObj (fun () ->
            match arming with
            | RecoveryArming.Armed -> ()
            | RecoveryArming.NotArmed -> arming <- RecoveryArming.Armed)

    /// Clear an unconsumed recovery slot (squash started, opportunity spent).
    member _.DisarmRecoverySlot() : unit =
        lock lockObj (fun () ->
            match arming with
            | RecoveryArming.Armed -> arming <- RecoveryArming.NotArmed
            | RecoveryArming.NotArmed -> ())

    /// True when a real failure has created an unconsumed recovery slot.
    member _.IsRecoveryArmed: bool =
        lock lockObj (fun () ->
            match arming with
            | RecoveryArming.Armed -> true
            | RecoveryArming.NotArmed -> false)

    member this.Snapshot: CompanionMemory = this.Memory
    member this.GetMemory() : CompanionMemory = this.Memory

    /// Diagnostic only — not the busy definition (see BloggerRuntimeState).
    member _.LastSendTask: Task<unit> option = lock lockObj (fun () -> lastSendTask)

    member this.WaitInFlightAsync() : Task =
        let tOpt = lock lockObj (fun () -> lastSendTask)

        match tOpt with
        | Some t -> t :> Task
        | None -> Task.FromResult(()) :> Task
