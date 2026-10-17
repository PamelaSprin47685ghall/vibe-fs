namespace Wanxiangshu.Process

open System.Threading.Tasks
open Wanxiangshu.Repository.Programming.Js

module JsSandbox =

    /// Park lifecycle state carried by the api closure. The park member reads
    /// and writes these fields through the same mutable object the workflow
    /// holds, so a parked program and the orchestrator observe one state.
    type ParkState =
        { mutable ParkRequested: bool
          mutable ParkPayload: obj
          mutable ParkSignalResolve: obj
          mutable ParkDeferredResolve: obj
          mutable ParkDeferredReject: obj
          mutable ParkStack: string
          mutable AbandonRequested: bool }

    /// Handle to a started sandbox program: the async IIFE promise is exposed
    /// (not awaited to completion) so a caller may hold it across calls, and the
    /// deadline reference is mutable so a caller may reset the async deadline.
    type JsRunHandle =
        { ProgramPromise: Task<string>
          DeadlineRef: obj
          Api: obj
          ParkState: ParkState }

    /// Sentinel family recognized by the wrapper (JS-019: prefix, never
    /// exception-message sniffing). `__jsAbandoned` is the ABANDONED protocol
    /// sentinel: rejecting the program promise with it unwinds the stack from
    /// the await point so native try/finally runs (PLAN S2 step 6 / algorithm F).
    val abandonedSentinel: string

    val wrapProgram: baseClassSource: string -> modelSource: string -> string

    val runSurface:
        baseClassSource: string ->
        modelSource: string ->
        api: obj ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
            Task<JsRunHandle>

    /// Start a wrapped program with an explicit park lifecycle state. The
    /// caller owns the ParkState and builds the api with park support.
    val runSurfaceWithPark:
        baseClassSource: string ->
        modelSource: string ->
        api: obj ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        parkState: ParkState option ->
            Task<JsRunHandle>

    val awaitResult: handle: JsRunHandle -> outputBoundBytes: int -> Task<Result<string, JsFailure>>

    val resetDeadline: handle: JsRunHandle -> deadlineEpochMs: int64 -> unit

    /// Mark the suspended model-source line with `>| ` (others ` | `).
    val sourceWithExecutionMarker: baseClassSource: string -> modelSource: string -> stack: string -> string

    /// Install the park signal the workflow races against the program promise.
    val installParkSignal: parkState: ParkState -> Task<obj>

    /// Resolve the deferred promise the program is awaiting at `await js.park`
    /// with the injected unpark value.
    val unpark: parkState: ParkState -> value: obj -> unit

    /// Reject the deferred promise with the `__jsAbandoned` marker so native
    /// try/finally blocks run as a finalizer.
    val abandon: parkState: ParkState -> unit
