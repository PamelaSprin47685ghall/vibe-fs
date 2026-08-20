namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
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
open Wanxiangshu.Process

/// Parking + dual request slots (ENFORCER-047/050/160).
///
/// CurrentRequest ownership = physical flight registry (HasFlight / TryGetFlight).
/// PendingOffer = the next Main material staged only while Parked (own slot).
/// Drain window = physical drain slot (GetDrainWindow / SetDrainWindow / IsDrainOpen).
type IParkedTransformHost =
    abstract ParkTransform: string * TimeSpan -> Task<bool>
    abstract ResumeParked: string -> bool
    abstract CancelParked: string -> unit
    abstract HasParked: string -> bool
    /// Physical single-flight: entry present = a Blogger request owns this session.
    abstract HasFlight: string -> bool
    abstract TryGetFlight: string -> BloggerRequestContext option
    abstract SetCurrentRequest: string * BloggerRequestContext -> unit
    abstract TryPeekCurrentRequest: string -> BloggerRequestContext option
    abstract ClearCurrentRequest: string -> unit
    /// Stage PendingOffer. Returns true when a parked waiter was resumed.
    abstract SetPendingOffer: string * BloggerRequestContext -> bool
    abstract HasPendingOffer: string -> bool
    abstract TryTakePendingOffer: string -> BloggerRequestContext option
    /// Physical drain-window slot.
    abstract GetDrainWindow: string -> DrainWindow
    abstract SetDrainWindow: string * DrainWindow -> unit
    abstract IsDrainOpen: string -> bool

/// One parkable transform wait for one session (ENFORCER-160).
/// Lifetime armed via ITimerPort.Delay; Cancel on settle (physical timer stays in Process).
type ParkedTransform(sessionId: string, lifetime: TimeSpan, ?timerPort: ITimerPort) as this =
    let timers = defaultArg timerPort (PtyTiming.nodeTimerPort ())

    let completion =
        TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)

    // DSL-MUTABLE: resource — one-shot settle latch for the transform wait
    let mutable settled = false
    // DSL-MUTABLE: resource — injectable deadline (cleared / Cancelled on settle)
    let mutable deadline: IDeadlineHandle option = None

    do
        let ms = max 1 (int lifetime.TotalMilliseconds)
        let handle = timers.Delay ms
        deadline <- Some handle

        let arm () =
            task {
                do! handle.Delay
                this.TrySettle false
            }
            :> Task

        arm () |> ignore

    member _.SessionId = sessionId

    member _.Completion: Task<bool> = completion.Task

    member private _.TrySettle(result: bool) =
        if not settled then
            settled <- true
            deadline |> Option.iter (fun h -> h.Cancel())
            deadline <- None
            completion.SetResult result

    member _.TryResume() = this.TrySettle true

    member _.TryCancel() = this.TrySettle false
