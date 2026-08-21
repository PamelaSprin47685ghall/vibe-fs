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
open Fable.Core.JsInterop
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
open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type ParkWake =
    | MaterialAvailable of BloggerRequestContext
    | Cancelled

[<RequireQualifiedAccess>]
type MaterialOfferDisposition =
    | Delivered
    | Staged

/// Parking + dual request slots (ENFORCER-047/050/160).
///
/// CurrentRequest ownership = physical flight registry (HasFlight / TryGetFlight).
/// PendingOffer = the next Main material staged only while Parked (own slot).
/// Drain window = physical drain slot (GetDrainWindow / SetDrainWindow / IsDrainOpen).
type IBloggerRuntimeHost =
    abstract ParkTransform: string -> Task<ParkWake>
    abstract CancelParked: string -> unit
    abstract HasParked: string -> bool
    /// Physical single-flight: entry present = a Blogger request owns this session.
    abstract HasFlight: string -> bool
    abstract TryGetFlight: string -> BloggerRequestContext option
    abstract SetCurrentRequest: string * BloggerRequestContext -> unit
    abstract TryPeekCurrentRequest: string -> BloggerRequestContext option
    abstract ClearCurrentRequest: string -> unit
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

    member _.TryResume(context: BloggerRequestContext) = this.TrySettle(ParkWake.MaterialAvailable context)

    member _.TryCancel() = this.TrySettle ParkWake.Cancelled
