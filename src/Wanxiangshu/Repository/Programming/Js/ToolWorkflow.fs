namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Process
open Wanxiangshu.Participant.Provider

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

    [<Emit("Number.isFinite($0)")>]
    let private jsIsFinite (value: obj) : bool = jsNative

    /// JS-010: a returned or delivered value is JSON-compatible — null, boolean,
    /// finite number, string, or an array/object of such values. An array holds
    /// no null element and is either all objects or one primitive tree.
    /// Validation stays on the native value; only the rendering boundary
    /// converts it into `LlmFacing.Data.Value` (that conversion cannot fail,
    /// so it must not run before the value is accepted).
    let rec private validateJsValue (value: obj) : Result<unit, JsFailure> =
        let ty = jsType value

        if jsNull value then Ok()
        elif ty = "boolean" || ty = "string" then Ok()
        elif ty = "number" then validateJsNumber value
        elif jsIsArray value then validateJsArray value
        elif ty = "object" then validateJsObject value
        else Error JsFailure.InvalidReturnValue

    and isPlainObject (value: obj) : bool =
        not (jsNull value) && jsType value = "object" && not (jsIsArray value)

    and validateJsNumber (value: obj) : Result<unit, JsFailure> =
        if jsIsFinite value then
            Ok()
        else
            Error JsFailure.InvalidReturnValue

    and isPrimitiveTreeValue (value: obj) : bool =
        let ty = jsType value

        if ty = "boolean" || ty = "string" then
            true
        elif ty = "number" then
            jsIsFinite value
        elif jsIsArray value then
            [ 0 .. jsLength value - 1 ]
            |> List.forall (fun index -> isPrimitiveTreeValue (jsGet value index))
        else
            false

    and validateAll (values: obj list) : Result<unit, JsFailure> =
        match values with
        | [] -> Ok()
        | value :: rest -> validateJsValue value |> Result.bind (fun () -> validateAll rest)

    and validateJsArray (value: obj) : Result<unit, JsFailure> =
        let items = [ 0 .. jsLength value - 1 ] |> List.map (fun index -> jsGet value index)

        validateAll items
        |> Result.bind (fun () ->
            if List.isEmpty items then
                Ok()
            elif List.exists jsNull items then
                Error JsFailure.InvalidReturnValue
            elif List.forall isPlainObject items then
                Ok()
            elif List.forall isPrimitiveTreeValue items then
                Ok()
            else
                Error JsFailure.InvalidReturnValue)

    and validateJsObject (value: obj) : Result<unit, JsFailure> =
        let keys = jsKeys value

        [ 0 .. keys.Length - 1 ]
        |> List.map (fun index -> jsGet value keys.[index])
        |> validateAll

    let private validateParsed (value: obj) : Result<obj, JsFailure> =
        validateJsValue value |> Result.map (fun () -> value)

    let parse (json: string) : Result<obj, JsFailure> =
        try
            validateParsed (JS.JSON.parse json)
        with _ ->
            Error JsFailure.InvalidReturnValue

    /// `this.deliver(reportMapping)` ends the program with a report
    /// mapping; the sandbox reports it as this sentinel instead of a
    /// return value.
    let private deliveredPrefix = "{\"__jsDelivered\":"

    let isDelivered (json: string) = json.StartsWith deliveredPrefix

    let private presentView (view: obj) : Result<obj, JsFailure> =
        if isNull view then
            Error JsFailure.InvalidReturnValue
        else
            Ok view

    let private outcomeView (json: string) : Result<obj, JsFailure> =
        try
            presentView ((JS.JSON.parse json)?view)
        with _ ->
            Error JsFailure.InvalidReturnValue

    /// One decode for both endings: a delivered program's value is the view
    /// declaration it handed over. The declaration is validated as a view
    /// at the deliver gate; a deliver without a view is not a delivery.
    let parseOutcome (json: string) : Result<obj, JsFailure> =
        if isDelivered json then outcomeView json else parse json

/// The task a delivery serves. The durable witness names the same three
/// references; this layer states them in its own vocabulary because it runs
/// below the host turn and cannot reach the host's witness type.
[<RequireQualifiedAccess>]
type DeliveryTaskReference =
    | ChildWork of handle: string
    | Invocation of invocationId: string
    | Lifecycle of workId: string * incumbencyId: string

/// The host's attribution of one tool call: the task the call serves, the
/// logical run and the authority it belongs to, and the prose the model
/// wrote before the call. The settle point cannot derive any of it — it runs
/// below the host turn, where only the call fence is visible.
type DeliveryAttribution =
    { LogicalRunId: LogicalRunId
      AuthorityRoot: AuthorityRootUserMessageId
      PhysicalUserMessageId: PhysicalUserMessageId
      Task: DeliveryTaskReference
      RetirementId: string option
      FormalText: string }

/// The delivery witness of one settled call. The host writer maps it onto
/// the durable witness fact — adding the material digest — and commits it;
/// this layer never touches the journal itself.
type DeliveryWitness =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRoot: AuthorityRootUserMessageId
      PhysicalUserMessageId: PhysicalUserMessageId
      ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      Task: DeliveryTaskReference
      FormalText: string
      ViewDeclaration: string
      View: string
      RetirementId: string option }

/// The narrow durable capability the settle point uses to record a delivery
/// witness. A refused commit means the delivery is not established: the
/// caller must learn that instead of reading a success nobody can prove.
type IDeliveryWitnessWriter =
    abstract CommitDeliveryWitness: witness: DeliveryWitness -> Task<Result<unit, string>>

/// What the settle point needs to attribute a delivery and to make its
/// evidence durable. A call without a settlement has no durable store behind
/// it (test/dev harness): the process-live cell is then the only evidence.
[<RequireQualifiedAccess>]
type DeliverySettlement =
    | Attributed of attribution: DeliveryAttribution * writer: IDeliveryWitnessWriter
    | Refused of reason: string

/// JS-085: one js-* tool invocation, end to end — the only orchestration of
/// sandbox execution, as-is per-write persistence, durable accounting and the
/// commit fact (JS-012/JS-013/JS-015). With `store = None` the workflow is
/// ephemeral (no durable facts — tests); with a store, the written mutations
/// are accounted as Prepared/Committed after the effects already landed.

module JsToolWorkflow =
    type ProcessHost =
        { HardLimit: System.TimeSpan
          Own: (unit -> Task) -> bool
          ReadOutput: unit -> Task<Result<unit, string>>
          AttachLifetimeAbort: (unit -> unit) -> Task -> (unit -> unit)
          AttachAbort: (unit -> unit) -> (unit -> unit) }

    let standaloneProcessHost attachAbort : ProcessHost =
        { HardLimit = JsCallLoop.standalone.HardLimit
          Own = JsCallLoop.standalone.Own
          ReadOutput = JsCallLoop.standalone.ReadOutput
          AttachLifetimeAbort = fun _ _ -> ignore
          AttachAbort = attachAbort }

    type FileAccessObservation = JsExplicitFileRead list -> string list -> Task<unit>

    let private outputNotice sessionId () =
        ProviderProse.render
            (ProviderLanguageBinding.forSessionText sessionId)
            "tool/js-program/run-output-truncated"
            Map.empty

    /// Outcome of one invocation: the program's structured value plus the
    /// commit report — or a stable JsFailure. `Parked` is the top-level parked
    /// payload: the program suspended at `await this.park(payload)` and the site
    /// is registered under `handle` (PLAN S2 step 4).
    type JsToolOutcome =
        | Succeeded of
            value: obj *
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
        | Parked of handle: string * payload: obj * source: string

    /// The park payload is created inside the sandbox realm; the host reads it
    /// from ParkState across the vm boundary. A JSON round-trip detaches the
    /// value from the vm prototypes so host-side consumers and semantic tests
    /// compare it structurally as a plain host-realm object.
    [<Emit("JSON.parse(JSON.stringify($0))")>]
    let private hostRealmPayload (payload: obj) : obj = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isStringValue (value: obj) : bool = jsNative

    /// The park signal wins the race by settling the host-synthesized marker
    /// string; the workflow reads it back as the parked marker rather than a
    /// program result.
    let private isParkedSentinel (value: obj) : bool =
        isStringValue value && (unbox<string> value).StartsWith "{\"__jsParked\":"

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
    let private recordFirstAppendFailure
        (currentFailure: JsFailure option)
        (appended: Result<'a, JsTransactionAppendFailure>)
        =
        match currentFailure, appended with
        | None, Error failure -> Some(JsFailure.TransactionPersistenceFailed failure)
        | _ -> currentFailure

    let private settleLedger (ledger: JsAsIsLedger) : Task<Result<unit, JsFailure>> =
        task {
            // DSL-MUTABLE: algorithm-scratch — first append failure for the outcome
            let mutable appendFailure = None

            for pending in ledger.Appends do
                let! appended = pending
                appendFailure <- recordFirstAppendFailure appendFailure appended

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


    let private checkReadOnlyMutation
        (hasMutationCapability: bool)
        (mutations: JsStagedMutation list)
        : JsFailure option =
        if not hasMutationCapability && not (List.isEmpty mutations) then
            Some JsFailure.ReadOnlyMutationRejected
        else
            None

    let private decodeViewDeclaration (raw: obj) : Result<obj option, JsFailure> =
        if isNull raw then
            Ok None
        else
            Wanxiangshu.Repository.Programming.Js.Anchor.AnchorSurface.parse raw
            |> Result.map Some

    let private parseRawView (json: string) : Result<obj, JsFailure> =
        try
            Ok((JS.JSON.parse json)?view)
        with _ ->
            Error(JsFailure.ViewDeclarationInvalid "delivered view did not parse")

    let private deliveredView (json: string) : Result<obj option, JsFailure> =
        if not (JsToolsData.isDelivered json) then
            Ok None
        else
            parseRawView json |> Result.bind decodeViewDeclaration

    let private parkedDeliveryFailure (sessionId: string) : JsFailure option =
        let parkedHandles = JsParkedSites.handlesForSession sessionId

        if List.isEmpty parkedHandles then
            None
        else
            let handles = System.String.Join(", ", parkedHandles)

            Some(
                JsFailure.ProgramFailed(
                    sprintf "delivery is blocked while this session has unresolved parked handles: %s" handles
                )
            )

    let private captureDelivered
        (deliveredJson: string option ref)
        (json: string)
        (parsed: Result<obj, JsFailure>)
        : Result<obj, JsFailure> =
        match deliveredView json with
        | Error failure -> Error failure
        | Ok _ ->
            deliveredJson.Value <- Some json
            parsed

    let private gateDelivered
        (deliveredJson: string option ref)
        (sessionId: string)
        (json: string)
        (parsed: Result<obj, JsFailure>)
        : Result<obj, JsFailure> =
        match parkedDeliveryFailure sessionId with
        | Some failure -> Error failure
        | None -> captureDelivered deliveredJson json parsed

    /// Algorithm G1: the deliver hard gate. A program cannot deliver while
    /// its own session still has unresolved parked sites; an unrelated
    /// session's park never blocks this delivery. On success the delivered
    /// sentinel JSON is captured in `deliveredJson` so the cell write can
    /// happen only after settlement.
    let private validateDeliverGate
        (deliveredJson: string option ref)
        (sessionId: string)
        (json: string)
        (parsed: Result<obj, JsFailure>)
        : Result<obj, JsFailure> =
        if not (JsToolsData.isDelivered json) then
            parsed
        else
            gateDelivered deliveredJson sessionId json parsed

    /// Clear only what this call produced (deliver clause 6). A delivery
    /// another call of the same session made is evidence and stays: a later
    /// failure in that session must not erase it.
    let private clearOwnDelivery (sessionId: string) (fence: (ProviderRunIdentity * ToolCallId) option) : unit =
        match fence with
        | None ->
            // Without a call fence the cell cannot be attributed to this
            // call, so nothing may be cleared on its behalf.
            ()
        | Some(providerRun, toolCallId) when
            JsDeliveredValues.peekFence sessionId
            |> Option.exists (fun cellFence -> cellFence.ProviderRun = providerRun && cellFence.ToolCallId = toolCallId)
            ->
            JsDeliveredValues.clear sessionId
        | Some _ -> ()

    /// The declared view as text: the exact declaration the program handed
    /// over, so the witness states the view and not a paraphrase of it.
    let private declarationText (view: obj) : string = JS.JSON.stringify view

    /// The delivery witness of a settled call: the fence of the delivering
    /// call, the host's attribution of that call, and the view it handed
    /// over. Every field comes from one of those three sources — nothing
    /// here is invented, and what the settle point cannot know is missing
    /// rather than filled in.
    let private fencedDeliveryWitness
        (sessionId: string)
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId)
        (attribution: DeliveryAttribution)
        (view: obj)
        (captured: JsDeliveredValues.CapturedDelivery)
        : Result<DeliveryWitness, JsFailure> =
        let declaration = declarationText view
        let material = captured.Text

        if System.String.IsNullOrWhiteSpace attribution.FormalText then
            Error(JsFailure.DeliveryNotEstablished "the delivering call has no attributed prose")
        else
            Ok
                { SessionId = SessionId.create sessionId
                  LogicalRunId = attribution.LogicalRunId
                  AuthorityRoot = attribution.AuthorityRoot
                  PhysicalUserMessageId = attribution.PhysicalUserMessageId
                  ProviderRun = providerRun
                  ToolCallId = toolCallId
                  Task = attribution.Task
                  FormalText = attribution.FormalText
                  ViewDeclaration = declaration
                  View = material
                  RetirementId = attribution.RetirementId }

    let private deliveryWitness
        (sessionId: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        (attribution: DeliveryAttribution)
        (view: obj)
        (captured: JsDeliveredValues.CapturedDelivery)
        : Result<DeliveryWitness, JsFailure> =
        match fence with
        | None -> Error(JsFailure.DeliveryNotEstablished("the delivering call has no fence to own the witness"))
        | Some(providerRun, toolCallId) ->
            fencedDeliveryWitness sessionId providerRun toolCallId attribution view captured

    let private recordDeliveredView
        (sessionId: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        (view: obj)
        (captured: JsDeliveredValues.CapturedDelivery)
        : unit =
        match fence with
        | Some(providerRun, toolCallId) ->
            JsDeliveredValues.publishCaptured sessionId providerRun toolCallId captured
            |> ignore
        | None -> JsDeliveredValues.recordView sessionId (Some view)

    let private commitWitness
        (record: unit -> unit)
        (writer: IDeliveryWitnessWriter)
        (witness: DeliveryWitness)
        : Task<Result<unit, JsFailure>> =
        task {
            let! committed = writer.CommitDeliveryWitness witness

            match committed with
            | Ok() ->
                record ()

                return Ok()
            | Error reason ->
                return
                    Error(
                        JsFailure.DeliveryNotEstablished(
                            "the durable delivery witness was not committed (" + reason + ")"
                        )
                    )
        }

    let private commitAttributedDelivery
        (sessionId: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        (record: unit -> unit)
        (attribution: DeliveryAttribution)
        (writer: IDeliveryWitnessWriter)
        (view: obj)
        (captured: JsDeliveredValues.CapturedDelivery)
        : Task<Result<unit, JsFailure>> =
        task {
            match deliveryWitness sessionId fence attribution view captured with
            | Error failure -> return Error failure
            | Ok witness -> return! commitWitness record writer witness
        }

    let private settleCapturedView
        (sessionId: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        (attributed: (DeliveryAttribution * IDeliveryWitnessWriter) option)
        (view: obj)
        (captured: JsDeliveredValues.CapturedDelivery)
        : Task<Result<unit, JsFailure>> =
        let record () =
            recordDeliveredView sessionId fence view captured

        match attributed with
        | None ->
            record ()
            Task.FromResult(Ok())
        | Some(attribution, writer) -> commitAttributedDelivery sessionId fence record attribution writer view captured

    let private captureAndSettleView
        (sessionId: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        (attributed: (DeliveryAttribution * IDeliveryWitnessWriter) option)
        (view: obj)
        : Task<Result<unit, JsFailure>> =
        task {
            let assistant =
                attributed |> Option.map (fun (attribution, _) -> attribution.FormalText)

            match JsDeliveredValues.captureMaterial sessionId view assistant with
            | None -> return Error(JsFailure.DeliveryNotEstablished "the materialized view is empty")
            | Some captured -> return! settleCapturedView sessionId fence attributed view captured
        }

    let private settleView sessionId fence settlement view =
        match settlement with
        | Some(DeliverySettlement.Refused reason) -> Task.FromResult(Error(JsFailure.DeliveryNotEstablished reason))
        | Some(DeliverySettlement.Attributed(attribution, writer)) ->
            captureAndSettleView sessionId fence (Some(attribution, writer)) view
        | None -> captureAndSettleView sessionId fence None view

    /// The delivery settlement of one settled call (deliver clauses 3 and 6).
    ///
    /// The delivered view is materialized first, then the durable witness is
    /// committed, and only a committed delivery records the process-live
    /// cell. A witness that cannot be committed means the delivery did not
    /// happen: the caller sees the failure and no success evidence is left
    /// behind. A call without a settlement has no durable store behind it
    /// (test/dev harness): the process-live cell is then its only evidence.
    ///
    /// A committed delivery is also a submission fact: the attempt ledger
    /// records it, so the transform hook's delivery adjudication can stop
    /// this attempt.
    let private settleDeliveredView
        (sessionId: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        (settlement: DeliverySettlement option)
        (json: string)
        : Task<Result<unit, JsFailure>> =
        task {
            match deliveredView json with
            | Error failure -> return Error failure
            | Ok None -> return Error(JsFailure.ViewDeclarationInvalid "the delivered view declaration is empty")
            | Ok(Some view) -> return! settleView sessionId fence settlement view
        }

    /// A successful run that delivered: it stands only once the delivered
    /// view settled. A delivery that did not establish presents as not
    /// established and leaves no success evidence behind (clause 6).
    let private settleDeliveredSuccess
        (sessionId: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        (settlement: DeliverySettlement option)
        (json: string)
        value
        rewritten
        created
        removed
        moved
        : Task<JsToolOutcome> =
        task {
            let! deliveryOutcome = settleDeliveredView sessionId fence settlement json

            match deliveryOutcome with
            | Ok() -> return Succeeded(value, rewritten, created, removed, moved)
            | Error failure ->
                clearOwnDelivery sessionId fence
                return Failed(failure, rewritten, created, removed, moved)
        }

    /// The delivered cell is written only after the tool settled
    /// successfully: a failed observation or ledger must not leave a cell
    /// that could pass as a delivery.
    let private settleDeliveryOf
        (sessionId: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        (settlement: DeliverySettlement option)
        (deliveredJson: string option ref)
        (settled: JsToolOutcome)
        : Task<JsToolOutcome> =
        task {
            match settled, deliveredJson.Value with
            | Succeeded(value, rewritten, created, removed, moved), None ->
                // An ordinary return is not a delivery (clause 1) and
                // leaves no delivery evidence of its own.
                return Succeeded(value, rewritten, created, removed, moved)
            | Succeeded(value, rewritten, created, removed, moved), Some json ->
                return! settleDeliveredSuccess sessionId fence settlement json value rewritten created removed moved
            | _ ->
                clearOwnDelivery sessionId fence
                return settled
        }


    let private decodeRunResult
        (deliveredJson: string option ref)
        (sessionId: string)
        (decoded: Result<string, JsFailure>)
        : Result<obj, JsFailure> =
        match decoded with
        | Error failure -> Error failure
        | Ok json ->
            JsToolsData.parseOutcome json
            |> validateDeliverGate deliveredJson sessionId json


    let private settleExecutionOutcome
        (observation: Result<unit, JsFailure>)
        (ledgerOutcome: Result<unit, JsFailure>)
        (value: obj)
        (rewritten: string list)
        (created: string list)
        (removed: string list)
        (moved: (string * string) list)
        : JsToolOutcome =
        match observation, ledgerOutcome with
        | Error failure, _ -> Failed(failure, rewritten, created, removed, moved)
        | _, Error failure -> Failed(failure, rewritten, created, removed, moved)
        | Ok(), Ok() -> Succeeded(value, rewritten, created, removed, moved)

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

    let private nameYieldPayload (parkState: JsSandbox.ParkState) name =
        if
            not (isNull parkState.ParkPayload)
            && emitJsExpr parkState.ParkPayload "$0.__jsYield === true"
        then
            parkState.ParkPayload?name <- name

    let private registerParkedSite
        (site: JsParkedSites.JsParkedSite)
        (lifecyclePersistence: IJsLifecyclePersistence option)
        (baseClassSource: string)
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState)
        (rewritten: string list)
        (created: string list)
        (removed: string list)
        (moved: (string * string) list)
        : JsToolOutcome =
        match JsParkedSites.register site with
        | Error failure ->
            site.Cancellation.Cancel()
            JsSandbox.abandon parkState
            Failed(failure, rewritten, created, removed, moved)
        | Ok _ ->
            recordLifecycle lifecyclePersistence JsLifecycleEventType.Parked site.Handle site.SessionId

            nameYieldPayload parkState (site.Name |> Option.defaultValue site.Handle)

            let source =
                JsSandbox.sourceWithExecutionMarker baseClassSource site.ModelSource parkState.ParkStack

            Parked(site.Handle, hostRealmPayload parkState.ParkPayload, source)

    let private registerResumedParkedSite
        (newSite: JsParkedSites.JsParkedSite)
        (baseClassSource: string)
        : JsToolOutcome =
        match JsParkedSites.register newSite with
        | Error failure ->
            newSite.Cancellation.Cancel()
            JsSandbox.abandon newSite.RunHandle.ParkState
            Failed(failure, [], [], [], [])
        | Ok _ ->
            nameYieldPayload newSite.RunHandle.ParkState (newSite.Name |> Option.defaultValue newSite.Handle)

            let source =
                JsSandbox.sourceWithExecutionMarker
                    baseClassSource
                    newSite.ModelSource
                    newSite.RunHandle.ParkState.ParkStack

            Parked(newSite.Handle, hostRealmPayload newSite.RunHandle.ParkState.ParkPayload, source)

    let private settleResumedExecutionOutcome (ledgerOutcome: Result<unit, JsFailure>) (value: obj) : JsToolOutcome =
        match ledgerOutcome with
        | Error failure -> Failed(failure, [], [], [], [])
        | Ok() -> Succeeded(value, [], [], [], [])

    /// Run a model program against root. `baseClassSource` is the generated
    /// JsProgram (JS-002); `modelSource` is the model's `class Js ... run()`.
    /// deadlineMs bounds the sandbox; outputBoundBytes bounds the result.
    /// `persistence` enables durable prepare/commit facts (JS-012). Current and
    /// transaction head are owned by the canonical Integrator; no history reader is exposed here.
    let private runCore
        (scopeApi: obj -> obj)
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

            let execution =
                { JsCallLoop.standalone with
                    OutputNotice = outputNotice ""
                    SynchronousBudgetMs = deadlineMs
                    RemainingMs =
                        fun () ->
                            Task.FromResult(
                                max 0L (deadlineEpochMs - System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                            ) }

            let children = new JsCallLoop.Host(root, execution)

            let! outcome =
                taskResult {
                    let runHost: JsToolsBindings.JsCallHost =
                        { Start = children.Start
                          Wait = children.Wait
                          SignalParked = children.SignalParked
                          SendParked = children.SendParked
                          TakeDelta = children.TakeDelta
                          Dispose = children.DisposeAsync
                          Loop =
                            fun parkState runnableClass stack ->
                                JsCallLoop.execute execution parkState root runnableClass stack }

                    let searchHost: JsToolsBindings.JsSearchHost =
                        { Search = JsSearch.execute execution root }

                    let api =
                        JsToolsBindings.createApi
                            capabilities
                            root
                            staging
                            readSnapshots
                            explicitReads
                            ledger
                            (Some runHost)
                            (Some searchHost)
                            None
                            None
                        |> scopeApi

                    let! resultJson =
                        task {
                            let! handle =
                                JsSandbox.runSurface baseClassSource modelSource api deadlineMs deadlineEpochMs

                            return! JsSandbox.awaitResult handle outputBoundBytes
                        }

                    let! value = JsToolsData.parseOutcome resultJson
                    return value
                }

            do! children.DisposeAsync()

            let mutations = staging |> Seq.toList
            let rewritten = rewrittenPaths mutations
            let created = createdPaths mutations
            let removed = removedPaths mutations
            let moved = movedPaths mutations
            let readPaths = explicitReads |> Seq.distinct |> Seq.toList
            let effectPaths = mutations |> List.map JsStagedMutation.path |> List.distinct

            let hasMutationCapability =
                Set.contains JsCapability.Write capabilities
                || Set.contains JsCapability.Remove capabilities
                || Set.contains JsCapability.Move capabilities

            let! ledgerOutcome = settleLedger ledger

            match outcome with
            | Error failure ->
                // Program execution or return-value parsing failed; the writes
                // that already landed stay on disk and are reported as-is.
                return Failed(failure, rewritten, created, removed, moved)
            | Ok _ when (checkReadOnlyMutation hasMutationCapability mutations).IsSome ->
                return Failed(JsFailure.ReadOnlyMutationRejected, rewritten, created, removed, moved)
            | Ok value ->
                let! observation = observeFileAccess fileAccessObservation readPaths effectPaths
                return settleExecutionOutcome observation ledgerOutcome value rewritten created removed moved
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
            id
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
            id
            capabilities
            root
            baseClassSource
            modelSource
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            persistence
            (Some fileAccessObservation)

    let runScoped
        scopeApi
        capabilities
        root
        baseClassSource
        modelSource
        deadlineMs
        deadlineEpochMs
        outputBoundBytes
        persistence
        fileAccessObservation
        =
        runCore
            scopeApi
            capabilities
            root
            baseClassSource
            modelSource
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            persistence
            fileAccessObservation

    /// Run a new program with park support. `sessionId` tags the site so
    /// session close can reap it. The park signal is installed before the
    /// program starts; if the program suspends at `await this.park(payload)` the
    /// signal wins the race and the workflow registers the site. A successful
    /// registration records the Parked lifecycle audit trace (best-effort).
    /// The buffers one run owns; `makeApi` decides which members see them.
    type private RunBuffers =
        { Staging: ResizeArray<JsStagedMutation>
          ReadSnapshots: ResizeArray<JsReadSnapshot>
          ExplicitReads: ResizeArray<JsExplicitFileRead>
          Ledger: JsAsIsLedger }

    let private runProgramCore
        (sessionId: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        (settlement: DeliverySettlement option)
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
        (processHost: ProcessHost option)
        (makeApi: RunBuffers -> Wanxiangshu.Process.JsSandbox.ParkState -> JsCallLoop.ExecutionScope -> obj)
        (name: string option)
        : Task<JsToolOutcome> =
        task {
            let cancellation = new System.Threading.CancellationTokenSource()

            let host =
                processHost
                |> Option.defaultWith (fun () -> standaloneProcessHost (fun _ -> ignore))

            let runHandleReady = TaskCompletionSource<JsSandbox.JsRunHandle>()
            // DSL-MUTABLE: cancellation — hold the current sandbox abort capability.
            let mutable abortHandle: JsSandbox.JsRunHandle option = None

            let detachAbort = host.AttachAbort cancellation.Cancel

            let execution: JsCallLoop.ExecutionScope =
                { Cancellation = cancellation.Token
                  HardLimit = host.HardLimit
                  SynchronousBudgetMs = deadlineMs
                  OutputNotice = outputNotice sessionId
                  ReadOutput = host.ReadOutput
                  BorrowDeadline =
                    fun duration ->
                        task {
                            let! handle = runHandleReady.Task
                            return handle.BorrowDeadline duration
                        }
                  RemainingMs =
                    fun () ->
                        task {
                            let! handle = runHandleReady.Task

                            return
                                emitJsExpr handle.DeadlineRef "BigInt(Math.max(0, Math.floor($0.current - Date.now())))"
                        }
                  Own = host.Own }
            // DSL-MUTABLE: algorithm-scratch — JS as-is write ledger. Every
            // edit/write is persisted atomically at call time; this buffer
            // records what was actually written, so any termination (throw,
            // timeout, output bound, park) keeps the already-applied mutations
            // observable and on disk.
            let staging = ResizeArray<JsStagedMutation>()
            let readSnapshots = ResizeArray<JsReadSnapshot>()
            let explicitReads = ResizeArray<JsExplicitFileRead>()
            let ledger = JsToolsBindings.createLedger persistence

            let buffers: RunBuffers =
                { Staging = staging
                  ReadSnapshots = readSnapshots
                  ExplicitReads = explicitReads
                  Ledger = ledger }

            // DSL-MUTABLE: resource — park lifecycle state shared by the api
            // member and the workflow. Process-live, never durable.
            let parkState: Wanxiangshu.Process.JsSandbox.ParkState =
                { ParkRequested = false
                  ParkPayload = null
                  ParkSignalResolve = null
                  ParkDeferredResolve = null
                  ParkDeferredReject = null
                  ParkStack = null
                  AbandonRequested = false }

            let programSettled = TaskCompletionSource<unit>()

            let detachLifetime =
                processHost
                |> Option.map (fun host ->
                    host.AttachLifetimeAbort
                        (fun () ->
                            abortHandle
                            |> Option.iter (fun handle ->
                                JsSandbox.resetDeadline
                                    handle
                                    (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                                     + int64 (max 1000 deadlineMs)))

                            cancellation.Cancel()
                            JsSandbox.abandon parkState)
                        (programSettled.Task :> Task))
                |> Option.defaultValue ignore

            let api = makeApi buffers parkState execution

            // Install the park signal the workflow races against the program
            // promise before the program starts: the async IIFE body runs
            // synchronously to its first await, so an immediate
            // `await this.park(payload)` must already see ParkSignalResolve.
            // Fable has no Task.WhenAny; Promise.race is the established
            // pattern (Await.fs / FinalityController).
            let parkSignal = JsSandbox.installParkSignal parkState

            let! runHandle =
                JsSandbox.runSurfaceWithPark baseClassSource modelSource api deadlineMs deadlineEpochMs (Some parkState)

            abortHandle <- Some runHandle
            runHandleReady.SetResult runHandle

            let taggedProgram: Task<obj> =
                task {
                    try
                        let! decoded = JsSandbox.awaitResult runHandle outputBoundBytes
                        let! _ = settleLedger ledger
                        return box (Choice1Of2 decoded)
                    finally
                        JsParkedSites.removeForState parkState
                        detachLifetime ()
                        cancellation.Cancel()
                        cancellation.Dispose()
                        programSettled.SetResult()
                }

            let taggedPark: Task<obj> =
                task {
                    let! payload = parkSignal
                    return box (Choice2Of2 payload)
                }

            let! winnerObj = emitJsExpr (taggedProgram, taggedPark) "Promise.race([$0, $1])": Task<obj>
            detachAbort ()

            // DSL-MUTABLE: algorithm-scratch — delivered sentinel JSON of this
            // run; the cell is written only after settlement succeeds.
            let deliveredJson = ref None

            let outcome =
                match unbox<Choice<Result<string, JsFailure>, obj>> winnerObj with
                | Choice1Of2 decoded ->
                    // The program finished first (Done, or a failure/timeout
                    // sentinel). Decode it like the plain path.
                    decodeRunResult deliveredJson sessionId decoded
                | Choice2Of2 _ ->
                    // Park won: the program suspended at `await this.park`.
                    Ok(box "{\"__jsParked\":true}")

            let mutations = staging |> Seq.toList
            let rewritten = rewrittenPaths mutations
            let created = createdPaths mutations
            let removed = removedPaths mutations
            let moved = movedPaths mutations
            let readPaths = explicitReads |> Seq.distinct |> Seq.toList
            let effectPaths = mutations |> List.map JsStagedMutation.path |> List.distinct

            let hasMutationCapability =
                Set.contains JsCapability.Write capabilities
                || Set.contains JsCapability.Remove capabilities
                || Set.contains JsCapability.Move capabilities

            match outcome with
            | Error failure ->
                // The program failed after the race; drain the fired ledger
                // (H1) so no append task is left unsettled. The program
                // failure keeps the reported code; landed writes stay as-is.
                let! _ = settleLedger ledger
                do! JsToolsBindings.disposeCallHost api
                // Only this call's own delivery evidence may go: a delivery
                // another call of this session made stays (clause 6).
                clearOwnDelivery sessionId fence
                return Failed(failure, rewritten, created, removed, moved)
            | Ok parkedValue when isParkedSentinel parkedValue ->
                // The program parked. Register the site under a fresh handle.
                // Named children stay alive in the api closure until the run finishes.
                let handle =
                    name
                    |> Option.map JsParkedSites.allocateNamedHandle
                    |> Option.defaultWith JsParkedSites.allocateHandle

                let site: JsParkedSites.JsParkedSite =
                    { Handle = handle
                      Name = name
                      RunHandle = runHandle
                      ReadSnapshots = readSnapshots
                      Ledger = ledger
                      SessionId = sessionId
                      Cancellation = cancellation
                      ModelSource = modelSource }

                return
                    registerParkedSite
                        site
                        lifecyclePersistence
                        baseClassSource
                        parkState
                        rewritten
                        created
                        removed
                        moved
            | Ok _ when (checkReadOnlyMutation hasMutationCapability mutations).IsSome ->
                // The refused change is not a delivery, but the delivery
                // another call of this session made is still evidence.
                clearOwnDelivery sessionId fence
                do! JsToolsBindings.disposeCallHost api
                return Failed(JsFailure.ReadOnlyMutationRejected, rewritten, created, removed, moved)
            | Ok value ->
                let! observation = observeFileAccess fileAccessObservation readPaths effectPaths
                let! ledgerOutcome = settleLedger ledger
                do! JsToolsBindings.disposeCallHost api

                let settled =
                    settleExecutionOutcome observation ledgerOutcome value rewritten created removed moved

                // The delivered cell is written only after the tool settled
                // successfully: a failed observation or ledger must not leave
                // a cell that could pass as a delivery.
                return! settleDeliveryOf sessionId fence settlement deliveredJson settled
        }

    /// The generated-surface api factory: bindings own the file members and
    /// the host loop.
    let private bindingsApiFactory
        (reviewCheck: (unit -> Task<bool>) option)
        (sessionId: string)
        (capabilities: Set<JsCapability>)
        (root: string)
        (buffers: RunBuffers)
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState)
        (execution: JsCallLoop.ExecutionScope)
        : obj =
        let children = new JsCallLoop.Host(root, execution)

        let runHost: JsToolsBindings.JsCallHost =
            { Start = children.Start
              Wait = children.Wait
              SignalParked = children.SignalParked
              SendParked = children.SendParked
              TakeDelta = children.TakeDelta
              Dispose = children.DisposeAsync
              Loop =
                fun parkState callableClass stack -> JsCallLoop.execute execution parkState root callableClass stack }

        let searchHost: JsToolsBindings.JsSearchHost =
            { Search = JsSearch.execute execution root }

        JsToolsBindings.createApiWithPark
            capabilities
            root
            buffers.Staging
            buffers.ReadSnapshots
            buffers.ExplicitReads
            buffers.Ledger
            parkState
            (Some runHost)
            (Some searchHost)
            (JsDelegationBridge.hostFor sessionId)
            (JsDelegationBridge.askFor sessionId)
        |> fun api ->
            reviewCheck
            |> Option.map (fun check -> JsReviewOnly.scopeReviewOnly check api)
            |> Option.defaultValue api

    let private runProgramWithPark
        (scopeApi: Wanxiangshu.Process.JsSandbox.ParkState -> obj -> obj)
        (reviewCheck: (unit -> Task<bool>) option)
        (sessionId: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        (settlement: DeliverySettlement option)
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
        (processHost: ProcessHost option)
        (name: string option)
        : Task<JsToolOutcome> =
        runProgramCore
            sessionId
            fence
            settlement
            capabilities
            root
            baseClassSource
            modelSource
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            persistence
            lifecyclePersistence
            fileAccessObservation
            processHost
            (fun buffers parkState execution ->
                bindingsApiFactory reviewCheck sessionId capabilities root buffers parkState execution
                |> scopeApi parkState)
            name


    /// Resume a parked site (algorithm E). `handle` must exist; the deferred
    /// promise is resolved with `value`, the handle is removed (one-shot), and
    /// the deadline is reset to now+10s. If the program parks again the result
    /// is a new parked payload with a new handle.
    let private unparkSite
        (site: JsParkedSites.JsParkedSite)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        (settlement: DeliverySettlement option)
        (value: obj)
        (baseClassSource: string)
        (deadlineMs: int)
        (outputBoundBytes: int)
        : Task<JsToolOutcome> =
        task {
            // Reset the deadline to now+10s (algorithm E5).
            let now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            Wanxiangshu.Process.JsSandbox.resetDeadline site.RunHandle (now + int64 (max 1000 deadlineMs))
            Wanxiangshu.Process.JsSandbox.unpark site.RunHandle.ParkState value
            JsParkedSites.remove site.Handle

            // Install a fresh park signal and race the program promise against
            // it: the resumed program may park again (algorithm E5).
            let parkSignal = JsSandbox.installParkSignal site.RunHandle.ParkState

            let taggedProgram: Task<obj> =
                task {
                    let! decoded = JsSandbox.awaitResult site.RunHandle outputBoundBytes
                    return box (Choice1Of2 decoded)
                }

            let taggedPark: Task<obj> =
                task {
                    let! payload = parkSignal
                    return box (Choice2Of2 payload)
                }

            let! winnerObj = emitJsExpr (taggedProgram, taggedPark) "Promise.race([$0, $1])": Task<obj>

            // DSL-MUTABLE: algorithm-scratch — delivered sentinel JSON of the
            // resumed run; the cell is written only after settlement succeeds.
            let deliveredJson = ref None

            let outcome =
                match unbox<Choice<Result<string, JsFailure>, obj>> winnerObj with
                | Choice1Of2 decoded -> task { return decodeRunResult deliveredJson site.SessionId decoded }
                | Choice2Of2 _ -> task { return Ok(box "{\"__jsParked\":true}") }

            let! outcome = outcome

            match outcome with
            | Error failure ->
                let! _ = settleLedger site.Ledger
                // Only this call's own delivery evidence may go: a delivery
                // another call of this session made stays (clause 6).
                clearOwnDelivery site.SessionId fence
                return Failed(failure, [], [], [], [])
            | Ok parkedValue when isParkedSentinel parkedValue ->
                // The resumed program parked again: a new handle and a new
                // parked payload (algorithm E5). The ledger stays with the
                // site: it is settled when the resumed run finishes.
                // The same api closure, and its process table, stay with the run.
                let newHandle =
                    site.Name
                    |> Option.map JsParkedSites.allocateNamedHandle
                    |> Option.defaultWith JsParkedSites.allocateHandle

                let newSite: JsParkedSites.JsParkedSite =
                    { Handle = newHandle
                      Name = site.Name
                      RunHandle = site.RunHandle
                      ReadSnapshots = site.ReadSnapshots
                      Ledger = site.Ledger
                      SessionId = site.SessionId
                      Cancellation = site.Cancellation
                      ModelSource = site.ModelSource }

                return registerResumedParkedSite newSite baseClassSource
            | Ok value ->
                let! ledgerOutcome = settleLedger site.Ledger
                let settled = settleResumedExecutionOutcome ledgerOutcome value

                return! settleDeliveryOf site.SessionId fence settlement deliveredJson settled
        }

    /// Abandon a parked site (algorithm F): reset the cleanup deadline, then
    /// reject the deferred promise with the `__jsAbandoned` marker so native
    /// try/finally runs within that window and await the program's settle.
    /// The outcome is Abandoned (no value); the site is removed.
    let private abandonSite
        (site: JsParkedSites.JsParkedSite)
        (deadlineMs: int)
        (outputBoundBytes: int)
        : Task<JsToolOutcome> =
        task {
            site.Cancellation.Cancel()

            // Reset the deadline to the 10s cleanup window (algorithm F2/F5).
            let now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            Wanxiangshu.Process.JsSandbox.resetDeadline site.RunHandle (now + int64 (max 1000 deadlineMs))
            Wanxiangshu.Process.JsSandbox.abandon site.RunHandle.ParkState

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

    /// Run a program whose api is supplied by the caller but whose park
    /// lifecycle, race and outcome rendering stay workflow-owned. The plan
    /// tenure sandbox uses this so `this.ask` suspends through the same site
    /// table as the generated surfaces.
    let runWithParkApi
        (sessionId: string)
        (baseClassSource: string)
        (modelSource: string)
        (makeApi: Wanxiangshu.Process.JsSandbox.ParkState -> obj)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        : Task<JsToolOutcome> =
        runProgramCore
            sessionId
            None
            None
            Set.empty
            ""
            baseClassSource
            modelSource
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            None
            None
            None
            None
            (fun _ parkState _ -> makeApi parkState)
            None

    /// Resume a parked site from any owning surface (generated or plan).
    let resumeParked
        (handle: string)
        (value: obj)
        (baseClassSource: string)
        (deadlineMs: int)
        (outputBoundBytes: int)
        : Task<JsToolOutcome> =
        match JsParkedSites.tryFind handle with
        | None -> Task.FromResult(Failed(JsFailure.HandleUnknown handle, [], [], [], []))
        | Some site -> unparkSite site None None value baseClassSource deadlineMs outputBoundBytes

    /// Abandon a parked site from any owning surface.
    let abandonParked (handle: string) (deadlineMs: int) (outputBoundBytes: int) : Task<JsToolOutcome> =
        match JsParkedSites.tryFind handle with
        | None -> Task.FromResult(Failed(JsFailure.HandleUnknown handle, [], [], [], []))
        | Some site -> abandonSite site deadlineMs outputBoundBytes

    [<RequireQualifiedAccess>]
    type private ToolWorkflowInvocation =
        | Program of source: string * name: string option
        | Unpark of handle: string * value: obj
        | Abandon of handle: string
        | Invalid of reason: string

    [<Emit("$0 !== null && $0 !== undefined")>]
    let private isPresent (value: obj) : bool = jsNative

    /// The states a js-* input carries, in protocol order. A state is present
    /// when its key holds a value; null and undefined are absences.
    let private inputStates (input: obj) : (string * obj) list =
        if isNull input then
            []
        else
            [ "program"; "unpark"; "abandon" ]
            |> List.choose (fun key ->
                let value = input?(key)
                if isPresent value then Some(key, value) else None)

    /// Why a js-* input is contradictory: it carries more than one of
    /// program / unpark / abandon. The reason names the conflicting states.
    let private conflictReason (states: (string * obj) list) : string =
        let names = states |> List.map fst |> String.concat ", "

        "program, unpark and abandon are mutually exclusive: a call carries exactly one of them (got "
        + names
        + ")"

    /// Why a js-* input is contradictory: it carries more than one of
    /// program / unpark / abandon. `None` when the input carries at most one
    /// state, so a caller can refuse a conflicting call before anything runs.
    let conflictingInputReason (input: obj) : string option =
        let states = inputStates input

        if List.length states > 1 then
            Some(conflictReason states)
        else
            None

    let private programInvocation source (given: obj) =
        if not (isPresent given) then
            ToolWorkflowInvocation.Program(string source, None)
        elif not (emitJsExpr given "typeof $0 === 'string'") then
            ToolWorkflowInvocation.Invalid "name must be a string"
        elif System.String.IsNullOrWhiteSpace(string given) then
            ToolWorkflowInvocation.Invalid "name must not be empty"
        elif JsParkedSites.isNameParked (string given) then
            ToolWorkflowInvocation.Invalid "name is already parked"
        else
            ToolWorkflowInvocation.Program(string source, Some(string given))

    let private parseWorkflowInvocation (input: obj) : ToolWorkflowInvocation =
        match inputStates input with
        | [] -> ToolWorkflowInvocation.Invalid "no program, unpark, or abandon input"
        | [ ("program", source) ] -> programInvocation source (input?name)
        | [ ("unpark", unpark) ] -> ToolWorkflowInvocation.Unpark(string (unpark?handle), unpark?value)
        | [ ("abandon", abandon) ] -> ToolWorkflowInvocation.Abandon(string (abandon?handle))
        | states -> ToolWorkflowInvocation.Invalid(conflictReason states)

    let private resolveParkedSite (sessionId: string) (handle: string) : Result<JsParkedSites.JsParkedSite, JsFailure> =
        match JsParkedSites.tryFind handle with
        | Some site when site.SessionId = sessionId -> Ok site
        | _ -> Error(JsFailure.HandleUnknown handle)

    let private dispatchResolvedParkedSite
        (resolved: Result<JsParkedSites.JsParkedSite, JsFailure>)
        (useSite: JsParkedSites.JsParkedSite -> Task<JsToolOutcome>)
        : Task<JsToolOutcome> =
        match resolved with
        | Error failure -> Task.FromResult(Failed(failure, [], [], [], []))
        | Ok site -> useSite site

    /// Execute one js-* tool invocation under the three-state input protocol
    /// (PLAN S2 step 5). The `input` object carries one of:
    ///   { program: string, name?: string }
    ///   { unpark: { handle: string, value: obj } }
    ///   { abandon: { handle: string } }
    /// `sessionId` tags newly created parked sites so session close can reap
    /// them. `lifecyclePersistence` records Parked/Unparked/Abandoned audit
    /// traces (best-effort; a failed audit never fails the tool call); Reaped
    /// is recorded by the session-close path (PluginSessionScope).
    let private executeInvocation
        (scopeApi: Wanxiangshu.Process.JsSandbox.ParkState -> obj -> obj)
        (reviewCheck: (unit -> Task<bool>) option)
        (settlement: DeliverySettlement option)
        (sessionId: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
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
        (processHost: ProcessHost option)
        : Task<JsToolOutcome> =
        task {
            let useParked (site: JsParkedSites.JsParkedSite) action =
                task {
                    let detach =
                        processHost
                        |> Option.map (fun host -> host.AttachAbort site.Cancellation.Cancel)
                        |> Option.defaultValue ignore

                    try
                        return! action site
                    finally
                        detach ()
                }

            match parseWorkflowInvocation input with
            | ToolWorkflowInvocation.Program(programSource, name) ->
                return!
                    runProgramWithPark
                        scopeApi
                        reviewCheck
                        sessionId
                        fence
                        settlement
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
                        processHost
                        name
            | ToolWorkflowInvocation.Unpark(handle, value) ->
                return!
                    dispatchResolvedParkedSite (resolveParkedSite sessionId handle) (fun site ->
                        recordLifecycle lifecyclePersistence JsLifecycleEventType.Unparked handle sessionId

                        useParked site (fun active ->
                            unparkSite active fence settlement value baseClassSource deadlineMs outputBoundBytes))
            | ToolWorkflowInvocation.Abandon handle ->
                return!
                    dispatchResolvedParkedSite (resolveParkedSite sessionId handle) (fun site ->
                        recordLifecycle lifecyclePersistence JsLifecycleEventType.Abandoned handle sessionId
                        useParked site (fun active -> abandonSite active deadlineMs outputBoundBytes))
            | ToolWorkflowInvocation.Invalid reason -> return Failed(JsFailure.InvalidArgument reason, [], [], [], [])
        }

    let private contextDeclaration (input: obj) =
        if isNull input then
            Ok None
        elif isPresent (input?view) then
            Error(JsFailure.ViewDeclarationInvalid "use prefixUntil and thenMap, not view")
        elif not (isPresent (input?prefixUntil)) && not (isPresent (input?thenMap)) then
            Ok None
        else
            createObj [ "prefixUntil" ==> input?prefixUntil; "thenMap" ==> input?thenMap ]
            |> Anchor.AnchorDeclarationRule.parse
            |> Result.map Some

    let private commitContextDeclaration sessionId declaration outcome =
        match outcome, declaration with
        | (Succeeded _ | Parked _), Some accepted ->
            Anchor.AnchorLedger.setView sessionId accepted
            outcome
        | _ -> outcome

    let private executeCore
        scopeApi
        reviewCheck
        settlement
        sessionId
        fence
        capabilities
        root
        baseClassSource
        (input: obj)
        deadlineMs
        deadlineEpochMs
        outputBoundBytes
        persistence
        lifecyclePersistence
        fileAccessObservation
        processHost
        =
        task {
            match contextDeclaration input with
            | Error failure -> return Failed(failure, [], [], [], [])
            | Ok declaration ->
                let! outcome =
                    executeInvocation
                        scopeApi
                        reviewCheck
                        settlement
                        sessionId
                        fence
                        capabilities
                        root
                        baseClassSource
                        input
                        deadlineMs
                        deadlineEpochMs
                        outputBoundBytes
                        persistence
                        lifecyclePersistence
                        fileAccessObservation
                        processHost

                return commitContextDeclaration sessionId declaration outcome
        }

    /// Execute with the host's review check and durable delivery settlement.
    let executeWithReviewCheckAndDelivery
        reviewCheck
        settlement
        sessionId
        fence
        capabilities
        root
        baseClassSource
        (input: obj)
        deadlineMs
        deadlineEpochMs
        outputBoundBytes
        persistence
        lifecyclePersistence
        fileAccessObservation
        processHost
        =
        executeCore
            (fun _ api -> api)
            reviewCheck
            settlement
            sessionId
            fence
            capabilities
            root
            baseClassSource
            input
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            persistence
            lifecyclePersistence
            fileAccessObservation
            processHost

    let executeWithReviewCheck
        reviewCheck
        sessionId
        fence
        capabilities
        root
        baseClassSource
        (input: obj)
        deadlineMs
        deadlineEpochMs
        outputBoundBytes
        persistence
        lifecyclePersistence
        fileAccessObservation
        processHost
        =
        executeCore
            (fun _ api -> api)
            reviewCheck
            None
            sessionId
            fence
            capabilities
            root
            baseClassSource
            input
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            persistence
            lifecyclePersistence
            fileAccessObservation
            processHost

    let executeWithApiScope
        scopeApi
        settlement
        sessionId
        fence
        capabilities
        root
        baseClassSource
        (input: obj)
        deadlineMs
        deadlineEpochMs
        outputBoundBytes
        persistence
        lifecyclePersistence
        fileAccessObservation
        processHost
        =
        executeCore
            scopeApi
            None
            settlement
            sessionId
            fence
            capabilities
            root
            baseClassSource
            input
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            persistence
            lifecyclePersistence
            fileAccessObservation
            processHost

    let execute
        sessionId
        fence
        capabilities
        root
        baseClassSource
        (input: obj)
        deadlineMs
        deadlineEpochMs
        outputBoundBytes
        persistence
        lifecyclePersistence
        fileAccessObservation
        processHost
        =
        executeWithReviewCheck
            None
            sessionId
            fence
            capabilities
            root
            baseClassSource
            input
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            persistence
            lifecyclePersistence
            fileAccessObservation
            processHost

/// JS-016: stable LLM-visible result shapes, rendered as Synthetic TOML
/// (the only rendering owner; ARCH-010). Success is `# ok` plus data/fs;
/// failure is `# failed` plus code/reason.
module JsToolsResult =

    let private failureFields =
        function
        | JsFailure.RunOutputBudgetTooSmall minimum -> [ LlmFacing.Data.int64Field "minimumOutputBudgetBytes" minimum ]
        | _ -> []

    let render (outcome: JsToolWorkflow.JsToolOutcome) : string =
        match outcome with
        | JsToolWorkflow.JsToolOutcome.Succeeded(value, rewritten, created, removed, moved) ->
            LlmFacing.instruction "ok"
            |> LlmFacing.withData (
                LlmFacing.Data.structuredValue (LlmFacing.Data.ofJs value)
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
                @ failureFields failure
                @ effects
            )
            |> LlmFacing.render
        | JsToolWorkflow.JsToolOutcome.Parked(handle, payload, source) ->
            // Top-level parked payload (PLAN S2 step 4): the program suspended
            // at `await this.park(payload)` and the site is registered under
            // handle. The caller resumes with `{ unpark: { handle, value } }`
            // or gives up with `{ abandon: { handle } }`.
            LlmFacing.instruction "parked"
            |> LlmFacing.withData (
                [ LlmFacing.Data.stringField "handle" handle
                  LlmFacing.Data.stringField "source" source ]
                @ LlmFacing.Data.rootStructuredValue (LlmFacing.Data.ofJs payload)
            )
            |> LlmFacing.render
