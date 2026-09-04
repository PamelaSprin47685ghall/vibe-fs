namespace Wanxiangshu.Context.Companion.Blogger.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Git

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// ENFORCER-*: Blogger continuation parking, physical flight ownership and
/// drain windows for one plugin instance. Parked transforms are per-session
/// serial (the dictionary entry is the guard); flights live in SharedState
/// because they must be visible across worktree/root instances.
type PluginBloggerScope() =
    let shutdown = new CancellationTokenSource()
    /// ENFORCER-160/162: parked continuation transforms, keyed by session id.
    ///
    /// At most one parked transform per session (a session's step loop is
    /// serial, so two parks for one session cannot race in practice — the
    /// dictionary entry is the guard that makes the invariant structural).
    let parkedGate = obj ()
    /// DSL-cross-callback-proof: physical waiter — ParkedTransform owns a TaskCompletionSource transport fence
    // DSL-MUTABLE: resource — parked continuation transform registry by session id
    let parked = Dictionary<string, ParkedTransform>()
    // ENFORCER-047/050: dual slots without dual storage for PendingOffer.
    // CurrentRequest ownership = physical flight registry (entry = in-flight).
    // PendingOffer = separate dictionary for the next Main material while Parked.
    /// DSL-cross-callback-proof: physical — one-shot inbound material buffer owned by Blogger convergence
    // DSL-MUTABLE: resource — pending offer registry by session id
    let pendingOffer = Dictionary<string, BloggerRequestContext>()
    // Physical Blogger flight ownership lives in SharedState (cross worktree/root).
    /// DSL-cross-callback-proof: physical quiescence-permit — DrainWindow.Open carries an unforgeable DrainPermit
    // DSL-MUTABLE: single-flight — physical drain-window slot
    let drainWindows = Dictionary<string, DrainWindow>()

    let parkExistingOrCreate sessionId =
        match parked.TryGetValue sessionId with
        | true, existing -> existing.Completion
        | false, _ ->
            let created = ParkedTransform(sessionId)
            parked.[sessionId] <- created
            created.Completion

    interface IBloggerRuntimeHost with
        member _.Cancellation = shutdown.Token

        member this.ParkTransform(sessionId: string) : Task<ParkWake> =
            lock parkedGate (fun () ->
                match pendingOffer.TryGetValue sessionId with
                | true, context ->
                    pendingOffer.Remove sessionId |> ignore
                    Task.FromResult(ParkWake.MaterialAvailable context)
                | false, _ -> parkExistingOrCreate sessionId)

        member this.CancelParked(sessionId: string) : unit =
            lock parkedGate (fun () ->
                match parked.TryGetValue sessionId with
                | true, entry ->
                    entry.TryCancel()
                    parked.Remove sessionId |> ignore
                | false, _ -> ()

                pendingOffer.Remove sessionId |> ignore)

        member this.HasParked(sessionId: string) : bool =
            lock parkedGate (fun () -> parked.ContainsKey sessionId)

        // Physical flight ownership (PR7 knife 1): entry present = single-flight request.
        member this.HasFlight(sessionId: string) : bool =
            lock SharedState.BloggerFlightGate (fun () -> SharedState.BloggerFlights.ContainsKey sessionId)

        member this.TryGetFlight(sessionId: string) : BloggerRequestContext option =
            lock SharedState.BloggerFlightGate (fun () ->
                match SharedState.BloggerFlights.TryGetValue sessionId with
                | true, ctx -> Some ctx
                | false, _ -> None)

        member this.ClaimCurrentRequest(sessionId: string, context: BloggerRequestContext) : BloggerFlightClaim =
            lock SharedState.BloggerFlightGate (fun () ->
                match SharedState.BloggerFlights.TryGetValue sessionId with
                | false, _ ->
                    SharedState.BloggerFlights.Add(sessionId, context)
                    BloggerFlightClaim.Claimed
                | true, existing when
                    BloggerRequestContext.requestId existing = BloggerRequestContext.requestId context
                    ->
                    SharedState.BloggerFlights.[sessionId] <- context
                    BloggerFlightClaim.Refreshed
                | true, existing -> BloggerFlightClaim.Conflict(BloggerRequestContext.requestId existing))

        member this.TryPeekCurrentRequest(sessionId: string) : BloggerRequestContext option =
            lock SharedState.BloggerFlightGate (fun () ->
                match SharedState.BloggerFlights.TryGetValue sessionId with
                | true, ctx -> Some ctx
                | false, _ -> None)

        member this.ReleaseCurrentRequest(sessionId: string, requestId: BloggerRequestId) : BloggerFlightRelease =
            lock SharedState.BloggerFlightGate (fun () ->
                match SharedState.BloggerFlights.TryGetValue sessionId with
                | false, _ -> BloggerFlightRelease.Missing
                | true, existing when BloggerRequestContext.requestId existing = requestId ->
                    SharedState.BloggerFlights.Remove sessionId |> ignore
                    BloggerFlightRelease.Released
                | true, existing -> BloggerFlightRelease.Conflict(BloggerRequestContext.requestId existing))

        member _.AcquireMaterialization(sessionId: string) : Task<BloggerMaterializationLease> =
            SharedState.BloggerMaterializationAdmission.Acquire sessionId

        member this.OfferMaterial(sessionId: string, context: BloggerRequestContext) : MaterialOfferDisposition =
            lock parkedGate (fun () ->
                match parked.TryGetValue sessionId with
                | true, entry ->
                    parked.Remove sessionId |> ignore
                    entry.TryResume context
                    MaterialOfferDisposition.Delivered
                | false, _ ->
                    pendingOffer.[sessionId] <- context
                    MaterialOfferDisposition.Staged)

        member this.TryTakePendingOffer(sessionId: string) : BloggerRequestContext option =
            lock parkedGate (fun () ->
                match pendingOffer.TryGetValue sessionId with
                | true, context ->
                    pendingOffer.Remove sessionId |> ignore
                    Some context
                | false, _ -> None)

        member this.GetDrainWindow(sessionId: string) : DrainWindow =
            lock parkedGate (fun () -> this.GetDrainWindowUnlocked sessionId)

        member this.SetDrainWindow(sessionId: string, window: DrainWindow) : unit =
            lock parkedGate (fun () -> drainWindows.[sessionId] <- window)

        member this.IsDrainOpen(sessionId: string) : bool =
            lock parkedGate (fun () ->
                match this.GetDrainWindowUnlocked sessionId with
                | DrainWindow.Open _ -> true
                | DrainWindow.Closed -> false)

    member private _.GetDrainWindowUnlocked(sessionId: string) : DrainWindow =
        match drainWindows.TryGetValue sessionId with
        | true, window -> window
        | false, _ -> DrainWindow.Closed

    /// Session deletion drops the drain slot (unlike CancelParked, which
    /// preserves it). Mirrors DisposeSession's per-session cleanup.
    member _.DropDrainWindow(sessionId: string) =
        lock parkedGate (fun () -> drainWindows.Remove sessionId |> ignore)

    member _.BeginShutdown() =
        if not shutdown.IsCancellationRequested then
            shutdown.Cancel()

        lock parkedGate (fun () ->
            for entry in parked.Values |> Seq.toList do
                entry.TryCancel()

            parked.Clear()
            pendingOffer.Clear())

    /// ENFORCER-162: plugin dispose emits Cancelled to every material waiter.
    member this.Dispose() =
        this.BeginShutdown()

        lock parkedGate (fun () ->
            // BloggerFlights are SharedState — do not clear on one instance dispose.
            drainWindows.Clear())

        shutdown.Dispose()
