namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain

module internal ParkedTransformInterop =

    [<Emit("setTimeout($0, $1)")>]
    let setTimeoutJs (fn: unit -> unit) (ms: float) : obj = jsNative

    [<Emit("clearTimeout($0)")>]
    let clearTimeoutJs (handle: obj) : unit = jsNative

/// Parking surface. Offers stage a full BloggerRequestContext (ENFORCER-050/051),
/// never a bare string — cycle commit needs coverage baselines from the context.
type IParkedTransformHost =
    abstract ParkTransform: string * TimeSpan -> Task<bool>
    abstract OfferParked: string * BloggerRequestContext -> bool
    abstract ResumeParked: string -> bool
    abstract CancelParked: string -> unit
    abstract HasParked: string -> bool
    abstract TryConsumeStagedOffer: string -> BloggerRequestContext option

/// One parkable transform wait for one session (ENFORCER-160).
type ParkedTransform(sessionId: string, lifetime: TimeSpan) as this =
    let completion =
        TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)

    let mutable settled = false
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
