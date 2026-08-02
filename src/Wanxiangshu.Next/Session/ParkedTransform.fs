namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// JS timer interop. Fable's Task.Delay has no cancellation, and an unsettled
/// timer keeps the Node event loop alive — VERIFY-004 treats "criteria green
/// but the process refuses to exit" as a failure. Every park must therefore be
/// able to clear its own timer.
module internal ParkedTransformInterop =

    [<Emit("setTimeout($0, $1)")>]
    let setTimeoutJs (fn: unit -> unit) (ms: float) : obj = jsNative

    [<Emit("clearTimeout($0)")>]
    let clearTimeoutJs (handle: obj) : unit = jsNative

/// The parking surface a composition root exposes (implemented by
/// `PluginRuntimeScope`). An interface rather than plain members because Fable
/// compiles interface implementations to prototype methods — the only shape a
/// JS caller can reach without mangled names (VERIFY-008).
type IParkedTransformHost =
    /// Park a session's continuation transform; `true` = resumed,
    /// `false` = cancelled or timed out.
    abstract ParkTransform: string * TimeSpan -> Task<bool>
    /// Stage a fresh delta for a Blogger; resumes a parked transform.
    abstract OfferParked: string * string -> bool
    /// Resume without injection.
    abstract ResumeParked: string -> bool
    /// Cancel and release the waiter (ENFORCER-162).
    abstract CancelParked: string -> unit
    abstract HasParked: string -> bool
    abstract TryConsumeStagedOffer: string -> string option

/// One parkable transform wait for one session (SSOT/15 ENFORCER-160:
/// PendingTransform ≤ 1 per Companion; SSOT/14 STRENGTH-078 C-05/C-09).
///
/// The Host awaits every hook's promise (`plugin/index.ts:290`), so a transform
/// that awaits a ParkedTransform genuinely suspends this session's step loop:
/// no provider request leaves until Resume, Cancel, or the lifetime elapses.
/// Other sessions are unaffected — each step loop runs in its own fiber
/// (STRENGTH-078 C-04, ENFORCER-161).
///
/// Cancellation is explicit and resolves the waiter (STRENGTH-078 C-09 note:
/// nothing may rely on effect-interrupt semantics). A timeout settles the same
/// way as a cancel: the caller treats `false` as "parking failed" and proceeds
/// without the parked behaviour — fail closed, never hang.
type ParkedTransform(sessionId: string, lifetime: TimeSpan) as this =
    let completion =
        TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)

    let mutable settled = false
    let mutable timerHandle: obj option = None
    let mutable injection: string option = None

    do
        let ms = max 1.0 lifetime.TotalMilliseconds
        timerHandle <- Some(ParkedTransformInterop.setTimeoutJs (fun () -> this.TrySettle false) ms)

    member _.SessionId = sessionId

    /// The synthetic delta text a resume should inject (SSOT/15 ENFORCER-051:
    /// cycles after the first reach the Blogger as synthetic user material, not
    /// as a new PromptDispatcher side effect).
    member _.Injection = injection

    member _.SetInjection(text: string) = injection <- Some text

    member _.Completion: Task<bool> = completion.Task

    member private _.TrySettle(result: bool) =
        if not settled then
            settled <- true
            timerHandle |> Option.iter ParkedTransformInterop.clearTimeoutJs
            timerHandle <- None
            // Fable's TaskCompletionSource has SetResult (idempotent on a
            // settled promise); the settled flag makes the guard explicit.
            completion.SetResult result

    member _.TryResume() = this.TrySettle true

    member _.TryCancel() = this.TrySettle false
