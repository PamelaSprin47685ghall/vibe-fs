namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Process

/// Parse the sandbox JSON string into LlmFacing reference data and enforce JS-010.
module JsToolsData =

    [<Emit("$0 === null")>]
    let private jsNull (value: obj) : bool = jsNative

    [<Emit("typeof $0")>]
    let private jsType (value: obj) : string = jsNative

    [<Emit("Array.isArray($0)")>]
    let private jsIsArray (value: obj) : bool = jsNative

    [<Emit("Object.keys($0)")>]
    let private jsKeys (value: obj) : string[] = jsNative

    [<Emit("$0[$1]")>]
    let private jsGet (value: obj) (key: obj) : obj = jsNative

    [<Emit("$0.length")>]
    let private jsLength (value: obj) : int = jsNative

    [<Emit("Number.isInteger($0)")>]
    let private jsIsInteger (value: obj) : bool = jsNative

    [<Emit("Number.isFinite($0)")>]
    let private jsIsFinite (value: obj) : bool = jsNative

    let private maxSafeInteger = 9007199254740991.0

    let rec private isPrimitiveTree (value: LlmFacing.Data.Value) =
        match value with
        | LlmFacing.Data.Value.Bool _
        | LlmFacing.Data.Value.Integer _
        | LlmFacing.Data.Value.Float _
        | LlmFacing.Data.Value.String _ -> true
        | LlmFacing.Data.Value.Array items -> List.forall isPrimitiveTree items
        | LlmFacing.Data.Value.Null
        | LlmFacing.Data.Value.Object _ -> false

    let private validateArray (items: LlmFacing.Data.Value list) : Result<unit, JsFailure> =
        if
            items
            |> List.exists (function
                | LlmFacing.Data.Value.Null -> true
                | _ -> false)
        then
            Error JsFailure.InvalidReturnValue
        elif List.isEmpty items then
            Ok()
        elif
            items
            |> List.forall (function
                | LlmFacing.Data.Value.Object _ -> true
                | _ -> false)
        then
            Ok()
        elif List.forall isPrimitiveTree items then
            Ok()
        else
            Error JsFailure.InvalidReturnValue

    let private ofJsNumber (value: obj) : Result<LlmFacing.Data.Value, JsFailure> =
        if not (jsIsFinite value) then
            Error JsFailure.InvalidReturnValue
        elif not (jsIsInteger value) then
            Ok(LlmFacing.Data.Value.Float(unbox value))
        elif abs (unbox<float> value) <= maxSafeInteger then
            Ok(LlmFacing.Data.Value.Integer(int64 (unbox<float> value)))
        else
            Ok(LlmFacing.Data.Value.Float(unbox<float> value))

    let rec private ofJsValue (value: obj) : Result<LlmFacing.Data.Value, JsFailure> =
        let ty = jsType value

        if jsNull value then
            Ok LlmFacing.Data.Value.Null
        elif ty = "boolean" then
            Ok(LlmFacing.Data.Value.Bool(unbox value))
        elif ty = "string" then
            Ok(LlmFacing.Data.Value.String(unbox value))
        elif ty = "number" then
            ofJsNumber value
        elif jsIsArray value then
            ofJsArray value
        elif ty = "object" then
            ofJsObject value
        else
            Error JsFailure.InvalidReturnValue

    and ofJsArray (value: obj) : Result<LlmFacing.Data.Value, JsFailure> =
        result {
            let! items =
                [ 0 .. jsLength value - 1 ]
                |> List.traverseResultM (fun index -> ofJsValue (jsGet value index))

            do! validateArray items
            return LlmFacing.Data.Value.Array items
        }

    and ofJsObject (value: obj) : Result<LlmFacing.Data.Value, JsFailure> =
        result {
            let keys = jsKeys value

            let! fields =
                [ 0 .. keys.Length - 1 ]
                |> List.traverseResultM (fun index ->
                    ofJsValue (jsGet value keys.[index])
                    |> Result.map (fun item -> keys.[index], item))

            return LlmFacing.Data.Value.Object fields
        }

    let parse (json: string) : Result<LlmFacing.Data.Value, JsFailure> =
        try
            ofJsValue (JS.JSON.parse json)
        with _ ->
            Error JsFailure.InvalidReturnValue

    /// Extract the payload from a parked sentinel JSON string. The sentinel is
    /// produced by the park signal path; its payload is the value the program
    /// passed to `await js.park(payload)`.
    let parseParkedPayload (json: string) : obj =
        try
            let payload = JS.JSON.parse json
            jsGet payload "payload"
        with _ ->
            null

/// JS-085: one js-* tool invocation, end to end — the only orchestration of
/// sandbox execution, staging, preflight, durable prepare, commit and the
/// commit fact (JS-012/JS-013/JS-015). With `store = None` the workflow is
/// ephemeral (no durable facts — tests); with a store, the transaction is
/// Prepared BEFORE any filesystem effect and Committed AFTER, so crash
/// recovery can undo only what was provably written.

module JsToolWorkflow =

    type FileAccessObservation = JsExplicitFileRead list -> string list -> Task<unit>

    /// Outcome of one invocation: the program's structured value plus the
    /// commit report — or a stable JsFailure. `Parked` is the top-level parked
    /// payload: the program suspended at `await js.park(payload)` and the site
    /// is registered under `handle` (PLAN S2 step 4).
    type JsToolOutcome =
        | Succeeded of
            value: LlmFacing.Data.Value *
            rewritten: string list *
            created: string list *
            removed: string list *
            moved: (string * string) list
        | Failed of
            failure: JsFailure *
            rewritten: string list *
            created: string list *
            removed: string list *
            moved: (string * string) list
        | Parked of handle: string * payload: obj

    /// The park payload is created inside the sandbox realm; the host reads it
    /// from ParkState across the vm boundary. A JSON round-trip detaches the
    /// value from the vm prototypes so host-side consumers and semantic tests
    /// compare it structurally as a plain host-realm object.
    [<Emit("JSON.parse(JSON.stringify($0))")>]
    let private hostRealmPayload (payload: obj) : obj = jsNative

    let private rewrittenPaths (mutations: JsStagedMutation list) =
        mutations
        |> List.choose (function
            | JsStagedMutation.Rewrite(path, _, _) -> Some path
            | JsStagedMutation.Create _
            | JsStagedMutation.Remove _
            | JsStagedMutation.Move _ -> None)

    let private createdPaths (mutations: JsStagedMutation list) =
        mutations
        |> List.choose (function
            | JsStagedMutation.Create(path, _) -> Some path
            | JsStagedMutation.Rewrite _
            | JsStagedMutation.Remove _
            | JsStagedMutation.Move _ -> None)

    let private removedPaths (mutations: JsStagedMutation list) =
        mutations
        |> List.choose (function
            | JsStagedMutation.Remove(path) -> Some path
            | JsStagedMutation.Rewrite _
            | JsStagedMutation.Create _
            | JsStagedMutation.Move _ -> None)

    let private movedPaths (mutations: JsStagedMutation list) =
        mutations
        |> List.choose (function
            | JsStagedMutation.Move(fromPath, toPath) -> Some(fromPath, toPath)
            | JsStagedMutation.Rewrite _
            | JsStagedMutation.Create _
            | JsStagedMutation.Remove _ -> None)

    let private mapPersistenceFailure
        (operation: Task<Result<'a, JsTransactionAppendFailure>>)
        : Task<Result<'a, JsFailure>> =
        operation
        |> TaskValue.map (Result.mapError JsFailure.TransactionPersistenceFailed)

    /// Settle the H1 ledger the mutation members fired (PLAN algorithm H1):
    /// await every Prepared/Committed append. The members fire Prepared at
    /// call time (before the write lands) and Committed right after, at
    /// per-mutation granularity with no aggregation; the physical EventStore
    /// append chain settles within the same event-loop turn as the write. A
    /// failed append fails the tool result with the stable
    /// TRANSACTION_PREPARE_*/COMMIT_* codes while the landed as-is writes
    /// stay reported, never rolled back ([013]).
    let private settleLedger (ledger: JsAsIsLedger) : Task<Result<unit, JsFailure>> =
        task {
            let mutable appendFailure = None

            for pending in ledger.Appends do
                let! appended = pending

                if appendFailure.IsNone then
                    match appended with
                    | Error failure -> appendFailure <- Some(JsFailure.TransactionPersistenceFailed failure)
                    | Ok _ -> ()

            match appendFailure with
            | Some failure -> return Error failure
            | None -> return Ok()
        }

    let private invokeObservation observe readPaths effectPaths : Task<Result<unit, JsFailure>> =
        task {
            try
                do! observe readPaths effectPaths
            with _ ->
                ()

            return Ok()
        }

    let private observeFileAccess
        (observer: FileAccessObservation option)
        readPaths
        effectPaths
        : Task<Result<unit, JsFailure>> =
        match observer with
        | None -> Task.FromResult(Ok())
        | Some observe -> invokeObservation observe readPaths effectPaths

    /// Run a model program against root. `baseClassSource` is the generated
    /// JsProgram (JS-002); `modelSource` is the model's `class Js ... run()`.
    /// deadlineMs bounds the sandbox; outputBoundBytes bounds the result.
    /// `persistence` enables durable prepare/commit facts (JS-012). Current and
    /// transaction head are owned by the canonical Integrator; no history reader is exposed here.
    let private runCore
        (capabilities: Set<JsCapability>)
        (root: string)
        (baseClassSource: string)
        (modelSource: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (persistence: IJsTransactionPersistence option)
        (fileAccessObservation: FileAccessObservation option)
        : Task<JsToolOutcome> =
        task {
            // DSL-MUTABLE: algorithm-scratch — JS as-is write ledger. Every
            // edit/write is persisted atomically at call time; this buffer
            // records what was actually written, so any termination (throw,
            // timeout, output bound) keeps the already-applied mutations
            // observable and on disk.
            let staging = ResizeArray<JsStagedMutation>()
            let readSnapshots = ResizeArray<JsReadSnapshot>()
            let explicitReads = ResizeArray<JsExplicitFileRead>()
            let ledger = JsToolsBindings.createLedger persistence

            let! outcome =
                taskResult {
                    let api =
                        JsToolsBindings.createApi capabilities root staging readSnapshots explicitReads ledger

                    let! resultJson =
                        task {
                            let! handle =
                                JsSandbox.runSurface baseClassSource modelSource api deadlineMs deadlineEpochMs

                            return! JsSandbox.awaitResult handle outputBoundBytes
                        }

                    let! value = JsToolsData.parse resultJson
                    return value
                }

            let mutations = staging |> Seq.toList
            let rewritten = rewrittenPaths mutations
            let created = createdPaths mutations
            let removed = removedPaths mutations
            let moved = movedPaths mutations
            let readPaths = explicitReads |> Seq.distinct |> Seq.toList
            let effectPaths = mutations |> List.map JsStagedMutation.path |> List.distinct

            let hasMutationCapability =
                Set.contains JsCapability.Edit capabilities
                || Set.contains JsCapability.Write capabilities
                || Set.contains JsCapability.Remove capabilities
                || Set.contains JsCapability.Move capabilities

            let! ledgerOutcome = settleLedger ledger

            match outcome with
            | Error failure ->
                // Program execution or return-value parsing failed; the writes
                // that already landed stay on disk and are reported as-is.
                return Failed(failure, rewritten, created, removed, moved)
            | Ok value ->
                if not hasMutationCapability && not (List.isEmpty mutations) then
                    return Failed(JsFailure.ReadOnlyMutationRejected, rewritten, created, removed, moved)
                else
                    let! observation = observeFileAccess fileAccessObservation readPaths effectPaths

                    match observation with
                    | Error failure -> return Failed(failure, rewritten, created, removed, moved)
                    | Ok() ->
                        match ledgerOutcome with
                        | Error failure -> return Failed(failure, rewritten, created, removed, moved)
                        | Ok() -> return Succeeded(value, rewritten, created, removed, moved)
        }

    let run
        (capabilities: Set<JsCapability>)
        (root: string)
        (baseClassSource: string)
        (modelSource: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (persistence: IJsTransactionPersistence option)
        : Task<JsToolOutcome> =
        runCore
            capabilities
            root
            baseClassSource
            modelSource
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            persistence
            None

    let runWithFileAccessObservation
        (capabilities: Set<JsCapability>)
        (root: string)
        (baseClassSource: string)
        (modelSource: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (persistence: IJsTransactionPersistence option)
        (fileAccessObservation: FileAccessObservation)
        : Task<JsToolOutcome> =
        runCore
            capabilities
            root
            baseClassSource
            modelSource
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            persistence
            (Some fileAccessObservation)

    /// Best-effort lifecycle audit trace. A failed audit never fails the tool
    /// call: the parked site is process-live and the audit is only a trace.
    let private recordLifecycle
        (lifecyclePersistence: IJsLifecyclePersistence option)
        (eventType: JsLifecycleEventType)
        (handle: string)
        (sessionId: string)
        : unit =
        match lifecyclePersistence with
        | None -> ()
        | Some durable ->
            let event: JsLifecycleEvent =
                { EventType = eventType
                  Handle = handle
                  SessionId = sessionId
                  At = System.DateTimeOffset.UtcNow.ToString("o") }

            task {
                let! _ = durable.AppendLifecycleEvent event
                return ()
            }
            |> ignore

    /// Run a new program with park support. `sessionId` tags the site so
    /// session close can reap it. The park signal is installed before the
    /// program starts; if the program suspends at `await js.park(payload)` the
    /// signal wins the race and the workflow registers the site. A successful
    /// registration records the Parked lifecycle audit trace (best-effort).
    let private runProgramWithPark
        (sessionId: string)
        (capabilities: Set<JsCapability>)
        (root: string)
        (baseClassSource: string)
        (modelSource: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (persistence: IJsTransactionPersistence option)
        (lifecyclePersistence: IJsLifecyclePersistence option)
        (fileAccessObservation: FileAccessObservation option)
        : Task<JsToolOutcome> =
        task {
            // DSL-MUTABLE: algorithm-scratch — JS as-is write ledger. Every
            // edit/write is persisted atomically at call time; this buffer
            // records what was actually written, so any termination (throw,
            // timeout, output bound, park) keeps the already-applied mutations
            // observable and on disk.
            let staging = ResizeArray<JsStagedMutation>()
            let readSnapshots = ResizeArray<JsReadSnapshot>()
            let explicitReads = ResizeArray<JsExplicitFileRead>()
            let ledger = JsToolsBindings.createLedger persistence

            // DSL-MUTABLE: resource — park lifecycle state shared by the api
            // member and the workflow. Process-live, never durable.
            let parkState: Wanxiangshu.Process.JsSandbox.ParkState =
                { ParkRequested = false
                  ParkPayload = null
                  ParkSignalResolve = null
                  ParkDeferredResolve = null
                  ParkDeferredReject = null
                  AbandonRequested = false }

            let api =
                JsToolsBindings.createApiWithPark capabilities root staging readSnapshots explicitReads ledger parkState

            // Install the park signal the workflow races against the program
            // promise before the program starts: the async IIFE body runs
            // synchronously to its first await, so an immediate
            // `await js.park(payload)` must already see ParkSignalResolve.
            // Fable has no Task.WhenAny; Promise.race is the established
            // pattern (Await.fs / FinalityController).
            let parkSignal = JsSandbox.installParkSignal parkState

            let! runHandle =
                JsSandbox.runSurfaceWithPark baseClassSource modelSource api deadlineMs deadlineEpochMs (Some parkState)

            let taggedProgram: Task<obj> =
                task {
                    let! json = runHandle.ProgramPromise
                    return box (Choice1Of2 json)
                }

            let taggedPark: Task<obj> =
                task {
                    let! payload = parkSignal
                    return box (Choice2Of2 payload)
                }

            let! winnerObj = emitJsExpr (taggedProgram, taggedPark) "Promise.race([$0, $1])": Task<obj>

            let outcome =
                match unbox<Choice<string, obj>> winnerObj with
                | Choice1Of2 json ->
                    // The program finished first (Done, or a failure/timeout
                    // sentinel). Decode it like the plain path.
                    JsToolsData.parse json
                | Choice2Of2 _ ->
                    // Park won: the program suspended at `await js.park`.
                    Ok(LlmFacing.Data.Value.String "{\"__jsParked\":true}")

            let mutations = staging |> Seq.toList
            let rewritten = rewrittenPaths mutations
            let created = createdPaths mutations
            let removed = removedPaths mutations
            let moved = movedPaths mutations
            let readPaths = explicitReads |> Seq.distinct |> Seq.toList
            let effectPaths = mutations |> List.map JsStagedMutation.path |> List.distinct

            let hasMutationCapability =
                Set.contains JsCapability.Edit capabilities
                || Set.contains JsCapability.Write capabilities
                || Set.contains JsCapability.Remove capabilities
                || Set.contains JsCapability.Move capabilities

            let isParkedSentinel (value: LlmFacing.Data.Value) =
                match value with
                | LlmFacing.Data.Value.String text -> text.StartsWith "{\"__jsParked\":"
                | _ -> false

            match outcome with
            | Error failure ->
                // The program failed after the race; drain the fired ledger
                // (H1) so no append task is left unsettled. The program
                // failure keeps the reported code; landed writes stay as-is.
                let! _ = settleLedger ledger
                return Failed(failure, rewritten, created, removed, moved)
            | Ok parkedValue when isParkedSentinel parkedValue ->
                // The program parked. Register the site under a fresh handle.
                let handle = JsParkedSites.allocateHandle ()

                let site: JsParkedSites.JsParkedSite =
                    { Handle = handle
                      RunHandle = runHandle
                      ReadSnapshots = readSnapshots
                      Ledger = ledger
                      SessionId = sessionId }

                match JsParkedSites.register site with
                | Error failure -> return Failed(failure, rewritten, created, removed, moved)
                | Ok _ ->
                    // Record the Parked audit trace (best-effort; algorithm H3).
                    recordLifecycle lifecyclePersistence JsLifecycleEventType.Parked handle sessionId
                    return Parked(handle, hostRealmPayload parkState.ParkPayload)
            | Ok value ->
                if not hasMutationCapability && not (List.isEmpty mutations) then
                    return Failed(JsFailure.ReadOnlyMutationRejected, rewritten, created, removed, moved)
                else
                    let! observation = observeFileAccess fileAccessObservation readPaths effectPaths

                    match observation with
                    | Error failure -> return Failed(failure, rewritten, created, removed, moved)
                    | Ok() ->
                        let! ledgerOutcome = settleLedger ledger

                        match ledgerOutcome with
                        | Error failure -> return Failed(failure, rewritten, created, removed, moved)
                        | Ok() -> return Succeeded(value, rewritten, created, removed, moved)
        }

    /// Resume a parked site (algorithm E). `handle` must exist; the deferred
    /// promise is resolved with `value`, the handle is removed (one-shot), and
    /// the deadline is reset to now+10s. If the program parks again the result
    /// is a new parked payload with a new handle.
    let private unparkSite
        (site: JsParkedSites.JsParkedSite)
        (value: obj)
        (deadlineMs: int)
        (outputBoundBytes: int)
        : Task<JsToolOutcome> =
        task {
            Wanxiangshu.Process.JsSandbox.unpark site.RunHandle.ParkState value
            JsParkedSites.remove site.Handle

            // Reset the deadline to now+10s (algorithm E5).
            let now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            Wanxiangshu.Process.JsSandbox.resetDeadline site.RunHandle (now + int64 (max 1000 deadlineMs))

            // Install a fresh park signal and race the program promise against
            // it: the resumed program may park again (algorithm E5).
            let parkSignal = JsSandbox.installParkSignal site.RunHandle.ParkState

            let taggedProgram: Task<obj> =
                task {
                    let! json = site.RunHandle.ProgramPromise
                    return box (Choice1Of2 json)
                }

            let taggedPark: Task<obj> =
                task {
                    let! payload = parkSignal
                    return box (Choice2Of2 payload)
                }

            let! winnerObj = emitJsExpr (taggedProgram, taggedPark) "Promise.race([$0, $1])": Task<obj>

            let outcome =
                match unbox<Choice<string, obj>> winnerObj with
                | Choice1Of2 json -> JsToolsData.parse json
                | Choice2Of2 _ -> Ok(LlmFacing.Data.Value.String "{\"__jsParked\":true}")

            let isParkedSentinel (value: LlmFacing.Data.Value) =
                match value with
                | LlmFacing.Data.Value.String text -> text.StartsWith "{\"__jsParked\":"
                | _ -> false

            match outcome with
            | Error failure ->
                let! _ = settleLedger site.Ledger
                return Failed(failure, [], [], [], [])
            | Ok parkedValue when isParkedSentinel parkedValue ->
                // The resumed program parked again: a new handle and a new
                // parked payload (algorithm E5). The ledger stays with the
                // site: it is settled when the resumed run finishes.
                let newHandle = JsParkedSites.allocateHandle ()

                let newSite: JsParkedSites.JsParkedSite =
                    { Handle = newHandle
                      RunHandle = site.RunHandle
                      ReadSnapshots = site.ReadSnapshots
                      Ledger = site.Ledger
                      SessionId = site.SessionId }

                match JsParkedSites.register newSite with
                | Error failure -> return Failed(failure, [], [], [], [])
                | Ok _ -> return Parked(newHandle, hostRealmPayload site.RunHandle.ParkState.ParkPayload)
            | Ok value ->
                let! ledgerOutcome = settleLedger site.Ledger

                match ledgerOutcome with
                | Error failure -> return Failed(failure, [], [], [], [])
                | Ok() -> return Succeeded(value, [], [], [], [])
        }

    /// Abandon a parked site (algorithm F): reject the deferred promise with
    /// the `__jsAbandoned` marker so native try/finally runs, reset the
    /// deadline to the 10s cleanup window, then await the program's settle.
    /// The outcome is Abandoned (no value); the site is removed.
    let private abandonSite
        (site: JsParkedSites.JsParkedSite)
        (deadlineMs: int)
        (outputBoundBytes: int)
        : Task<JsToolOutcome> =
        task {
            Wanxiangshu.Process.JsSandbox.abandon site.RunHandle.ParkState

            // Reset the deadline to the 10s cleanup window (algorithm F2/F5).
            let now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            Wanxiangshu.Process.JsSandbox.resetDeadline site.RunHandle (now + int64 (max 1000 deadlineMs))

            // Await the program's settle; the ABANDONED rejection unwinds the
            // stack so finally blocks run. A timeout force-truncates (F5).
            let! _ = JsSandbox.awaitResult site.RunHandle outputBoundBytes

            // Drain the ledger the finalizer fired (H1): the Abandoned
            // outcome is final regardless, so a failed append is only
            // observed here, never escalated into a different code.
            let! _ = settleLedger site.Ledger

            JsParkedSites.remove site.Handle

            return Failed(JsFailure.Abandoned, [], [], [], [])
        }

    /// Execute one js-* tool invocation under the three-state input protocol
    /// (PLAN S2 step 5). The `input` object carries one of:
    ///   { program: string }
    ///   { unpark: { handle: string, value: obj } }
    ///   { abandon: { handle: string } }
    /// `sessionId` tags newly created parked sites so session close can reap
    /// them. `lifecyclePersistence` records Parked/Unparked/Abandoned audit
    /// traces (best-effort; a failed audit never fails the tool call); Reaped
    /// is recorded by the session-close path (PluginSessionScope).
    let execute
        (sessionId: string)
        (capabilities: Set<JsCapability>)
        (root: string)
        (baseClassSource: string)
        (input: obj)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (persistence: IJsTransactionPersistence option)
        (lifecyclePersistence: IJsLifecyclePersistence option)
        (fileAccessObservation: FileAccessObservation option)
        : Task<JsToolOutcome> =
        task {
            let programText =
                if isNull input then
                    None
                else
                    match input?program with
                    | null -> None
                    | value -> Some(string value)

            let unparkHandle =
                if isNull input || isNull (input?unpark) then
                    None
                else
                    let unpark = input?unpark
                    Some(string (unpark?handle), unpark?value)

            let abandonHandle =
                if isNull input || isNull (input?abandon) then
                    None
                else
                    Some(string ((input?abandon)?handle))

            match programText with
            | Some programSource ->
                return!
                    runProgramWithPark
                        sessionId
                        capabilities
                        root
                        baseClassSource
                        programSource
                        deadlineMs
                        deadlineEpochMs
                        outputBoundBytes
                        persistence
                        lifecyclePersistence
                        fileAccessObservation
            | None ->
                match unparkHandle with
                | Some(handle, value) ->
                    match JsParkedSites.tryFind handle with
                    | None -> return Failed(JsFailure.HandleUnknown handle, [], [], [], [])
                    | Some site ->
                        // Record the Unparked audit trace (best-effort).
                        recordLifecycle lifecyclePersistence JsLifecycleEventType.Unparked handle sessionId
                        return! unparkSite site value deadlineMs outputBoundBytes
                | None ->
                    match abandonHandle with
                    | Some handle ->
                        match JsParkedSites.tryFind handle with
                        | None -> return Failed(JsFailure.HandleUnknown handle, [], [], [], [])
                        | Some site ->
                            // Record the Abandoned audit trace (best-effort).
                            recordLifecycle lifecyclePersistence JsLifecycleEventType.Abandoned handle sessionId
                            return! abandonSite site deadlineMs outputBoundBytes
                    | None ->
                        return Failed(JsFailure.InvalidArgument "no program, unpark, or abandon input", [], [], [], [])
        }

/// JS-016: stable LLM-visible result shapes, rendered as Synthetic TOML
/// (the only rendering owner; ARCH-010). Success is `# ok` plus data/fs;
/// failure is `# failed` plus code/reason.
module JsToolsResult =

    let render (outcome: JsToolWorkflow.JsToolOutcome) : string =
        match outcome with
        | JsToolWorkflow.JsToolOutcome.Succeeded(value, rewritten, created, removed, moved) ->
            LlmFacing.instruction "ok"
            |> LlmFacing.withData (
                LlmFacing.Data.structuredValue value
                @ LlmFacing.Data.fileEffects rewritten created removed moved
            )
            |> LlmFacing.render
        | JsToolWorkflow.JsToolOutcome.Failed(failure, rewritten, created, removed, moved) ->
            let effects =
                if
                    List.isEmpty rewritten
                    && List.isEmpty created
                    && List.isEmpty removed
                    && List.isEmpty moved
                then
                    []
                else
                    LlmFacing.Data.fileEffects rewritten created removed moved

            LlmFacing.instruction "failed"
            |> LlmFacing.withData (
                [ LlmFacing.Data.stringField "code" (JsFailure.code failure)
                  LlmFacing.Data.stringField "reason" (JsFailure.reason failure) ]
                @ effects
            )
            |> LlmFacing.render
        | JsToolWorkflow.JsToolOutcome.Parked(handle, payload) ->
            // Top-level parked payload (PLAN S2 step 4): the program suspended
            // at `await js.park(payload)` and the site is registered under
            // handle. The caller resumes with `{ unpark: { handle, value } }`
            // or gives up with `{ abandon: { handle } }`.
            LlmFacing.instruction "parked"
            |> LlmFacing.withData (
                [ LlmFacing.Data.stringField "handle" handle ]
                @ LlmFacing.Data.rootStructuredValue (LlmFacing.Data.ofJs payload)
            )
            |> LlmFacing.render
