namespace Wanxiangshu.Process

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Repository.Programming.Js

/// JS-011/JS-053 sandbox: arbitrary model JavaScript runs in a Node vm
/// context created with DONT_CONTEXTIFY. Host values (api returns, errors,
/// promises, Callable instances) cross a membrane into sandbox-realm copies
/// so `constructor.constructor` cannot reach the real Node process. Transfer
/// globals are deleted before user code runs; the deadline is checked with
/// the host clock inside the membrane, not a forgeable sandbox Date.now.
module JsSandbox =

    /// Park lifecycle state carried by the api closure. The park member reads
    /// and writes these fields through the same mutable object the workflow
    /// holds, so a parked program and the orchestrator observe one state.
    ///
    /// `ParkSignalResolve` is the park-signal resolver the workflow installs
    /// before each race: the park member calls it with the payload when the
    /// program suspends, completing the signal the workflow races against the
    /// program promise. `ParkDeferredResolve` / `ParkDeferredReject` are the
    /// deferred-promise resolvers the park member creates: `unpark` calls
    /// resolve with the injected value (the program continues as if the await
    /// returned it); `abandon` calls reject with the `__jsAbandoned` marker so
    /// native try/finally runs. `AbandonRequested` marks the cleanup window
    /// during which park is refused.
    type ParkState =
        {
            mutable ParkRequested: bool
            mutable ParkPayload: obj
            /// The park-signal resolver (a JS function) installed before each
            /// race; the park member calls it with the payload to complete the
            /// signal the workflow races against. `null` before install.
            mutable ParkSignalResolve: obj
            /// The deferred-promise resolver the park member creates; `unpark`
            /// calls it with the injected value. `null` while not parked.
            mutable ParkDeferredResolve: obj
            /// The deferred-promise rejecter the park member creates; `abandon`
            /// calls it with the `__jsAbandoned` marker. `null` while not parked.
            mutable ParkDeferredReject: obj
            /// The JS stack captured when the park member fired. The workflow
            /// uses it to mark the model-source line that is currently
            /// suspended; null when no stack was captured.
            mutable ParkStack: string
            mutable AbandonRequested: bool
        }

    /// Handle to a started sandbox program: the async IIFE promise is exposed
    /// (not awaited to completion) so a caller may hold it across calls, and
    /// the deadline reference is mutable so a caller may reset the async
    /// deadline without re-wrapping the program.
    type JsRunHandle =
        { ProgramPromise: Task<string>
          DeadlineRef: obj
          Api: obj
          ParkState: ParkState }

    [<Import("Buffer", "node:buffer")>]
    let private nodeBuffer: obj = jsNative

    [<Emit("JSON.parse($0)")>]
    let private parseJson (text: string) : obj = jsNative

    [<Emit("$0.current = Number($1)")>]
    let private setDeadline (deadlineRef: obj) (epochMs: int64) : unit = jsNative

    [<Import("Sandbox", "wanxiangli/sandbox")>]
    let private SandboxFactory: unit -> obj = jsNative

    let private sandboxLaw: obj =
        emitJsExpr (SandboxFactory) "$0()"

    /// Start the model program under wanxiangli/sandbox (DONT_CONTEXTIFY +
    /// realm membrane + afterEvaluate driver). Returns host DeadlineRef and
    /// ProgramPromise.
    let private launchMembraned
        (api: obj)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (baseClassSource: string)
        (modelSource: string)
        : obj =
        emitJsExpr
            (sandboxLaw, api, deadlineMs, deadlineEpochMs, baseClassSource, modelSource)
            """
(function(law, api, deadlineMs, deadlineEpochMs, baseClass, model) {
  var handle = law.run(api, Number(deadlineMs), Number(deadlineEpochMs), baseClass, model);
  return { DeadlineRef: handle.deadlineRef, ProgramPromise: handle.programPromise };
})($0, $1, $2, $3, $4, $5)
"""

    /// Sentinel payloads the wrapper returns instead of the program result;
    /// recognized by prefix, never by exception-message sniffing (JS-019).
    let private failedPrefix = "{\"__jsProgramFailed\":"
    let private hostFailedPrefix = "{\"__jsHostFailed\":"
    let private invalidReturnPrefix = "{\"__jsInvalidReturn\":"
    let private parkedPrefix = "{\"__jsParked\":"
    let private deliveredPrefix = "{\"__jsDelivered\":"

    /// The ABANDONED protocol sentinel. Rejecting the program promise with an
    /// error carrying this marker unwinds the program stack from its await
    /// point so native try/finally blocks run as a finalizer (PLAN S2 step 6 /
    /// algorithm F; Python GeneratorExit precedent). The marker is a stable
    /// prefix, not a message-text sniff.
    let abandonedSentinel = "__jsAbandoned"

    // The async IIFE that wraps base class + model source (sentinel encoding
    // of failures / park / delivered / invalid return) is owned by
    // wanxiangli/sandbox. The host no longer builds or exposes that string;
    // launchMembraned hands the two source strings to Sandbox().run.

    /// Classify a synchronous vm error: SyntaxError → INVALID_PROGRAM;
    /// vm timeout → PROGRAM_TIMEOUT; anything else → PROGRAM_FAILED.
    let private classifySyncError (error: obj) : JsFailure =
        let name = string (error?name)
        let message = string (error?message)

        if name = "SyntaxError" then
            JsFailure.InvalidProgram
        elif message.Contains "timed out" then
            JsFailure.ProgramTimeout
        else
            JsFailure.ProgramFailed message

    let private decodeHostFailed (json: string) : JsFailure =
        try
            let payload = parseJson json
            JsFailure.ofWire (string payload?code) (string payload?reason)
        with _ ->
            JsFailure.ProgramFailed "program threw"

    let private decodeProgramFailed (json: string) : JsFailure =
        try
            let payload = parseJson json
            JsFailure.ProgramFailed(string payload?message)
        with _ ->
            JsFailure.ProgramFailed "program threw"

    /// Decode the wrapper's JSON string into a typed result. `outputBoundBytes`
    /// bounds the serialized result (JS-054.2). Sentinel prefixes are matched
    /// before the byte bound so failures are never misreported as oversized.
    /// A parked sentinel is a normal outcome (the program suspended at
    /// `await this.park`), not a failure; it decodes to `Ok` with the sentinel
    /// JSON so the workflow can register the site.
    let private decodeRunResult (json: string) (outputBoundBytes: int) : Result<string, JsFailure> =
        if json.StartsWith hostFailedPrefix then
            Error(decodeHostFailed json)
        elif json.StartsWith failedPrefix then
            Error(decodeProgramFailed json)
        elif json.StartsWith invalidReturnPrefix then
            Error JsFailure.InvalidReturnValue
        elif json.StartsWith deliveredPrefix then
            Ok json
        elif json.StartsWith parkedPrefix then
            Ok json
        elif (nodeBuffer?byteLength (json, "utf8") |> unbox<int>) > outputBoundBytes then
            Error(JsFailure.ResultTooLarge None)
        else
            Ok json

    /// Start a wrapped program with an explicit park lifecycle state. The
    /// caller owns the ParkState and builds the api with park support
    /// (JsToolsBindings.createApiWithPark); this function injects both the api
    /// and the park state into the vm context.
    let runSurfaceWithPark
        (baseClassSource: string)
        (modelSource: string)
        (api: obj)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (parkState: ParkState option)
        : Task<JsRunHandle> =
        task {
            let state =
                match parkState with
                | Some state -> state
                | None ->
                    { ParkRequested = false
                      ParkPayload = null
                      ParkSignalResolve = null
                      ParkDeferredResolve = null
                      ParkDeferredReject = null
                      ParkStack = null
                      AbandonRequested = false }

            // Membrane launch: no host objects on the context global object;
            // ProgramPromise is a host-driven promise that drains afterEvaluate
            // microtasks so post-await sync loops still hit the vm timeout.
            let launched =
                launchMembraned api deadlineMs deadlineEpochMs baseClassSource modelSource

            let deadlineRef: obj = launched?DeadlineRef

            let promise: Task<string> =
                emitJsExpr (launched) "Promise.resolve($0.ProgramPromise)"

            return
                { ProgramPromise = promise
                  DeadlineRef = deadlineRef
                  Api = api
                  ParkState = state }
        }

    /// Start a wrapped program in a fresh vm context without awaiting it to
    /// completion. The async IIFE's promise is exposed as `ProgramPromise` so a
    /// caller may hold it across calls; the deadline is injected as a mutable
    /// reference (`DeadlineRef`) so a caller may reset the async deadline
    /// without re-wrapping. The vm context is held by the promise's closure
    /// chain while it remains unsettled — it is not explicitly lifted.
    ///
    /// `deadlineMs` bounds the synchronous segment via the vm timeout; the
    /// injected proxy bounds async segments (JS-054.1). Synchronous errors
    /// (SyntaxError, vm timeout) surface as a rejected `ProgramPromise` and are
    /// classified by `awaitResult`.
    let runSurface
        (baseClassSource: string)
        (modelSource: string)
        (api: obj)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        : Task<JsRunHandle> =
        runSurfaceWithPark baseClassSource modelSource api deadlineMs deadlineEpochMs None

    /// Await a started sandbox program and classify its outcome. Synchronous
    /// errors (SyntaxError → INVALID_PROGRAM, vm timeout → PROGRAM_TIMEOUT) are
    /// classified here; async failures arrive as sentinel JSON and are decoded
    /// by `decodeRunResult`.
    let awaitResult (handle: JsRunHandle) (outputBoundBytes: int) : Task<Result<string, JsFailure>> =
        task {
            try
                let! json = handle.ProgramPromise
                return decodeRunResult json outputBoundBytes
            with ex ->
                return Error(classifySyncError ex)
        }

    /// Classify one already-awaited wrapper JSON string (sentinel prefixes and
    /// output bound) without re-awaiting the program promise. The park race
    /// uses this so a program failure on the raced path decodes exactly like
    /// the plain path.
    let classifyResult (json: string) (outputBoundBytes: int) : Result<string, JsFailure> =
        decodeRunResult json outputBoundBytes

    /// Reset the async deadline of an in-flight sandbox program. Subsequent api
    /// member calls re-check `deadlineRef.current`, so the new epoch takes
    /// effect without re-wrapping or re-running the program.
    let resetDeadline (handle: JsRunHandle) (deadlineEpochMs: int64) : unit =
        setDeadline handle.DeadlineRef deadlineEpochMs

    /// The program-script line a stack frame reports, if the frame is one.
    ///
    /// The sandbox evaluates the program with `new Function`, so V8 prints a
    /// program frame as `at Js.run (eval at globalThis.__makeRunner
    /// (evalmachine.<anonymous>:226:19), <anonymous>:150:33)`: the trailing
    /// position is the line inside the evaluated program, while the
    /// `evalmachine.<anonymous>` position is the runner's own source. A
    /// frame that ran an evaluated *string* carries a nested `eval at` in its
    /// prefix, and its trailing position is that string's own line — a
    /// number that can coincide with a model line, so such a frame reports
    /// no line.
    let private programLineOfFrame (rawFrame: string) : int option =
        let frame = rawFrame.Trim()
        let marker = ", <anonymous>:"
        let at = frame.LastIndexOf(marker)
        let rest = if at < 0 then "" else frame.Substring(at + marker.Length)
        let colon = rest.IndexOf ':'
        let digits = if colon >= 0 then rest.Substring(0, colon) else rest
        let firstEvalAt = frame.IndexOf "eval at"
        let lastEvalAt = frame.LastIndexOf "eval at"
        let isProgramFrame =
            frame.StartsWith "at "
            && at >= 0
            && firstEvalAt >= 0
            && firstEvalAt = lastEvalAt

        match System.Int32.TryParse digits with
        | true, line when isProgramFrame -> Some line
        | _ -> None

    /// The model line a frame reports, when it is a program frame inside the
    /// model source.
    let private modelLineOfFrame (modelStart: int) (modelEnd: int) (frame: string) : int option =
        match programLineOfFrame frame with
        | Some line when line >= modelStart && line <= modelEnd -> Some(line - modelStart)
        | _ -> None

    /// The model line the program is suspended on, or -1 when no frame
    /// identifies one (no stack, no frame, or no frame inside the model
    /// source). A missing marker degrades to ` | ` on every line; it never
    /// marks a line that is not the suspended one.
    let private locateSuspendedLine (baseLines: int) (modelLines: int) (stack: string) : int =
        if isNull stack then
            -1
        else
            // Script line of the model's first line. The `new Function`
            // runner numbers the program from the reconstructed function
            // source, whose two header lines (`function anonymous(api,JSON`
            // and `) {`) precede the evaluated body, whose first line
            // `(async () => {` shares the third line with the `return ((`
            // prefix: the base class therefore starts at script line 4 and
            // the model source at `baseLines + 4`.
            let modelStart = baseLines + 4
            let modelEnd = modelStart + modelLines - 1

            stack.Split('\n')
            |> Array.tryPick (fun rawFrame -> modelLineOfFrame modelStart modelEnd rawFrame)
            |> Option.defaultValue -1

    /// Render the full model program with the suspended line marked. The
    /// closest program frame inside the model source is the model frame that
    /// called park (directly or through where). The marker is `>| ` on the
    /// suspended line and ` | ` everywhere else; a missing or unreadable
    /// stack degrades to ` | ` on every line, never to a failed park.
    let sourceWithExecutionMarker (baseClassSource: string) (modelSource: string) (stack: string) : string =
        let lines = modelSource.Split('\n')
        let currentLine =
            locateSuspendedLine (baseClassSource.Split('\n').Length) lines.Length stack

        lines
        |> Array.mapi (fun index line -> (if index = currentLine then ">| " else " | ") + line)
        |> String.concat "\n"
        |> fun marked -> marked + "\n"

    /// Install the park signal the workflow races against the program promise.
    /// The returned Task completes with the parked payload when the program's
    /// `park(payload)` member fires; the resolver is stored in
    /// `parkState.ParkSignalResolve` so the park member can call it.
    let installParkSignal (parkState: ParkState) : Task<obj> =
        parkState.ParkRequested <- false
        parkState.ParkPayload <- null
        parkState.ParkSignalResolve <- null

        let signal: obj =
            emitJsExpr
                (parkState)
                "(() => { const state = $0; return new Promise((resolve) => { state.ParkSignalResolve = resolve; }); })()"

        signal :?> Task<obj>

    /// Suspend the program through the shared park channel: mark the request,
    /// fire the signal the workflow races against, and hand back the deferred
    /// promise the program awaits until unpark resolves it (or abandon
    /// rejects it). Callers own the abandon-window refusal and the stack
    /// capture so this stays the one suspension mechanism.
    let suspendProgram (parkState: ParkState) (payload: obj) : obj =
        parkState.ParkRequested <- true
        parkState.ParkPayload <- payload

        if not (isNull parkState.ParkSignalResolve) then
            emitJsExpr (parkState.ParkSignalResolve, payload) "$0($1)" |> ignore

        emitJsExpr
            parkState
            "(function(state) { return new Promise(function(resolve, reject) { state.ParkDeferredResolve = resolve; state.ParkDeferredReject = reject; }); })($0)"

    /// Resolve the deferred promise the program is awaiting at `await this.park`.
    /// Called by `unpark` with the injected value; the program continues as if
    /// the await returned that value.
    let unpark (parkState: ParkState) (value: obj) : unit =
        if not (isNull parkState.ParkDeferredResolve) then
            emitJsExpr (parkState.ParkDeferredResolve, value) "$0($1)" |> ignore

    /// Reject the deferred promise with the `__jsAbandoned` marker. Called by
    /// `abandon`/`reap`; the rejection unwinds the program stack from the await
    /// point so native try/finally blocks run as a finalizer.
    let abandon (parkState: ParkState) : unit =
        parkState.AbandonRequested <- true

        if not (isNull parkState.ParkDeferredReject) then
            emitJsExpr
                (parkState.ParkDeferredReject, abandonedSentinel)
                "$0(Object.assign(new Error($1), { __jsAbandoned: true, message: $1 }))"
            |> ignore
