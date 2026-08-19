namespace Wanxiangshu.Context.Companion.Blogger.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open System.Collections.Generic
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
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
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

    interface IParkedTransformHost with
        member this.ParkTransform(sessionId: string, lifetime: TimeSpan) : Task<bool> =
            task {
                let (entry, staged) =
                    lock parkedGate (fun () ->
                        match parked.TryGetValue sessionId with
                        | true, existing -> existing, false
                        | false, _ ->
                            let created = ParkedTransform(sessionId, lifetime)
                            parked.[sessionId] <- created

                            // ENFORCER-050 offer-first merge: PendingOffer staged
                            // while no transform was parked makes this park return
                            // immediately with `true`.
                            created, pendingOffer.ContainsKey sessionId)

                if staged then
                    entry.TryResume()

                let! resumed = entry.Completion

                lock parkedGate (fun () ->
                    match parked.TryGetValue sessionId with
                    | true, current when obj.ReferenceEquals(current, entry) -> parked.Remove sessionId |> ignore
                    | _ -> ())

                return resumed
            }

        member this.ResumeParked(sessionId: string) : bool =
            lock parkedGate (fun () ->
                match parked.TryGetValue sessionId with
                | true, entry ->
                    entry.TryResume()
                    parked.Remove sessionId |> ignore
                    true
                | false, _ -> false)

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

        member this.SetCurrentRequest(sessionId: string, context: BloggerRequestContext) : unit =
            lock SharedState.BloggerFlightGate (fun () -> SharedState.BloggerFlights.[sessionId] <- context)

        member this.TryPeekCurrentRequest(sessionId: string) : BloggerRequestContext option =
            lock SharedState.BloggerFlightGate (fun () ->
                match SharedState.BloggerFlights.TryGetValue sessionId with
                | true, ctx -> Some ctx
                | false, _ -> None)

        member this.ClearCurrentRequest(sessionId: string) : unit =
            lock SharedState.BloggerFlightGate (fun () -> SharedState.BloggerFlights.Remove sessionId |> ignore)

        member this.SetPendingOffer(sessionId: string, context: BloggerRequestContext) : bool =
            lock parkedGate (fun () ->
                pendingOffer.[sessionId] <- context

                match parked.TryGetValue sessionId with
                | true, entry ->
                    entry.TryResume()
                    parked.Remove sessionId |> ignore
                    true
                | false, _ -> false)

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

    /// ENFORCER-162: plugin dispose cancels every parked waiter. The resolved
    /// `false` releases each suspended transform so the Host's step loop can
    /// finish its current request cycle.
    member _.Dispose() =
        lock parkedGate (fun () ->
            for entry in parked.Values |> Seq.toList do
                entry.TryCancel()

            parked.Clear()
            pendingOffer.Clear()
            // BloggerFlights are SharedState — do not clear on one instance dispose.
            drainWindows.Clear())
