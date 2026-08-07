namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain

module internal ParkedTransformInterop =

    [<Emit("setTimeout($0, $1)")>]
    let setTimeoutJs (fn: unit -> unit) (ms: float) : obj = jsNative

    [<Emit("clearTimeout($0)")>]
    let clearTimeoutJs (handle: obj) : unit = jsNative

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
    abstract TryTakePendingOffer: string -> BloggerRequestContext option
    /// Physical drain-window slot.
    abstract GetDrainWindow: string -> DrainWindow
    abstract SetDrainWindow: string * DrainWindow -> unit
    abstract IsDrainOpen: string -> bool

/// One parkable transform wait for one session (ENFORCER-160).
type ParkedTransform(sessionId: string, lifetime: TimeSpan) as this =
    let completion =
        TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)

    // DSL-MUTABLE: resource — one-shot settle latch for the transform wait
    let mutable settled = false
    // DSL-MUTABLE: resource — JS timeout handle (cleared on settle)
    let mutable timerHandle: obj option = None

    do
        let ms = max 1.0 lifetime.TotalMilliseconds
        timerHandle <- Some(ParkedTransformInterop.setTimeoutJs (fun () -> this.TrySettle false) ms)

    member _.SessionId = sessionId

    member _.Completion: Task<bool> = completion.Task

    member private _.TrySettle(result: bool) =
        if not settled then
            settled <- true
            timerHandle |> Option.iter ParkedTransformInterop.clearTimeoutJs
            timerHandle <- None
            completion.SetResult result

    member _.TryResume() = this.TrySettle true

    member _.TryCancel() = this.TrySettle false
