namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open System.Threading
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
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
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type ParkWake =
    | MaterialAvailable of BloggerRequestContext
    | Cancelled

[<RequireQualifiedAccess>]
type MaterialOfferDisposition =
    | Delivered
    | Staged

[<RequireQualifiedAccess>]
type BloggerFlightClaim =
    | Claimed
    | Refreshed
    | Conflict of BloggerRequestId

[<RequireQualifiedAccess>]
type BloggerFlightRelease =
    | Released
    | Missing
    | Conflict of BloggerRequestId

type BloggerMaterializationLease internal (release: unit -> unit) =
    let gate = obj ()
    // DSL-MUTABLE: resource — one-shot materialization admission release latch
    let mutable released = false

    member _.Release() =
        let shouldRelease =
            lock gate (fun () ->
                if released then
                    false
                else
                    released <- true
                    true)

        if shouldRelease then
            release ()

type BloggerMaterializationAdmission() =
    let gate = obj ()

    let queues =
        System.Collections.Generic.Dictionary<
            string,
            System.Collections.Generic.Queue<TaskCompletionSource<BloggerMaterializationLease>>
         >()

    let rec release sessionId =
        let next =
            lock gate (fun () ->
                match queues.TryGetValue sessionId with
                | true, waiters when waiters.Count > 0 -> Some(waiters.Dequeue())
                | true, _ ->
                    queues.Remove sessionId |> ignore
                    None
                | false, _ -> None)

        match next with
        | Some waiter -> waiter.SetResult(BloggerMaterializationLease(fun () -> release sessionId))
        | None -> ()

    member _.Acquire(sessionId: string) : Task<BloggerMaterializationLease> =
        lock gate (fun () ->
            match queues.TryGetValue sessionId with
            | true, waiters ->
                let waiter =
                    TaskCompletionSource<BloggerMaterializationLease>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    )

                waiters.Enqueue waiter
                waiter.Task
            | false, _ ->
                queues.Add(sessionId, System.Collections.Generic.Queue())
                Task.FromResult(BloggerMaterializationLease(fun () -> release sessionId)))

/// Parking + dual request slots (ENFORCER-047/050/160).
///
/// CurrentRequest ownership = physical flight registry (HasFlight / TryGetFlight).
/// PendingOffer = the next Main material staged only while Parked (own slot).
/// Drain window = physical drain slot (GetDrainWindow / SetDrainWindow / IsDrainOpen).
type IBloggerRuntimeHost =
    abstract Cancellation: CancellationToken
    abstract ParkTransform: string -> Task<ParkWake>
    abstract CancelParked: string -> unit
    abstract HasParked: string -> bool
    /// Physical single-flight: entry present = a Blogger request owns this session.
    abstract HasFlight: string -> bool
    abstract TryGetFlight: string -> BloggerRequestContext option
    abstract ClaimCurrentRequest: string * BloggerRequestContext -> BloggerFlightClaim
    abstract TryPeekCurrentRequest: string -> BloggerRequestContext option
    abstract ReleaseCurrentRequest: string * BloggerRequestId -> BloggerFlightRelease
    abstract AcquireMaterialization: string -> Task<BloggerMaterializationLease>
    abstract OfferMaterial: string * BloggerRequestContext -> MaterialOfferDisposition
    abstract TryTakePendingOffer: string -> BloggerRequestContext option
    /// Physical drain-window slot.
    abstract GetDrainWindow: string -> DrainWindow
    abstract SetDrainWindow: string * DrainWindow -> unit
    abstract IsDrainOpen: string -> bool

/// One event wait for one Blogger continuation.
type ParkedTransform(sessionId: string) as this =
    let completion =
        TaskCompletionSource<ParkWake>(TaskCreationOptions.RunContinuationsAsynchronously)

    // DSL-MUTABLE: resource — one-shot settle latch for the transform wait
    let mutable settled = false

    member _.SessionId = sessionId

    member _.Completion: Task<ParkWake> = completion.Task

    member private _.TrySettle(result: ParkWake) =
        if not settled then
            settled <- true
            completion.SetResult result

    member _.TryResume(context: BloggerRequestContext) =
        this.TrySettle(ParkWake.MaterialAvailable context)

    member _.TryCancel() = this.TrySettle ParkWake.Cancelled
