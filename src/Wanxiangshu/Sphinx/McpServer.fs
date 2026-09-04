namespace Wanxiangshu.Sphinx

open System
open System.Threading.Tasks
open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Resources
open Wanxiangshu.Sphinx.Core

open Fable.Core
open Fable.Core.JsInterop

/// MCP SDK + zod + stdio transport live only here. Every continuation
/// converges on SessionStore.ResumeObservation; this module never judges
/// phase, action keys, or observation legality itself.
module McpServer =

    [<Import("McpServer", "@modelcontextprotocol/sdk/server/mcp.js")>]
    let private mcpServerConstructor: obj = jsNative

    [<Import("StdioServerTransport", "@modelcontextprotocol/sdk/server/stdio.js")>]
    let private stdioTransportConstructor: obj = jsNative

    [<Import("z", "zod")>]
    let private zod: obj = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Import("resolve", "node:path")>]
    let private resolve (path: string) : string = jsNative

    [<Emit("new $0($1, $2)")>]
    let private construct (constructor: obj) (info: obj) (options: obj) : obj = jsNative

    [<Emit("new $0()")>]
    let private constructEmpty (constructor: obj) : obj = jsNative

    let private zString (description: string) : obj =
        emitJsExpr zod "$0.string().describe($1)"

    let private zNumberRecord (description: string) : obj =
        emitJsExpr (zod, description) "$0.record($0.string(), $0.number()).describe($1)"

    let private zRecord (description: string) : obj =
        emitJsExpr (zod, description) "$0.record($0.string(), $0.any()).describe($1)"

    let private zStringArray (description: string) : obj =
        emitJsExpr (zod, description) "$0.array($0.string()).describe($1)"

    let private zRecordArray (description: string) : obj =
        emitJsExpr (zod, description) "$0.array($0.record($0.string(), $0.any())).describe($1)"

    let private zAnyArray (description: string) : obj =
        emitJsExpr (zod, description) "$0.array($0.any()).describe($1)"

    let private zObjectArray (shape: obj) (description: string) : obj =
        emitJsExpr (zod, shape, description) "$0.array($0.object($1)).describe($2)"

    let private zNumber () : obj = emitJsExpr zod "$0.number()"

    let private zBareString () : obj = emitJsExpr zod "$0.string()"

    let private zBareStringArray () : obj = emitJsExpr zod "$0.array($0.string())"

    let private zOptional (schema: obj) : obj = emitJsExpr schema "$0.optional()"

    [<Emit("$0.registerTool($1, $2, $3)")>]
    let private registerTool (server: obj) (name: string) (config: obj) (handler: obj) : obj = jsNative

    [<Emit("(args) => $0(args)")>]
    let private unaryHandler (handler: obj -> obj) : obj = jsNative

    // WHAT[EPI-030]: the MCP SDK awaits whatever a tool callback returns, so a
    // Fable Task (a native promise) lets durable tools append after the session
    // accepted, without fire-and-forget. Rejections never escape: every durable
    // handler catches its failures into a typed isError result.
    [<Emit("(args) => $0(args)")>]
    let private asyncUnaryHandler (handler: obj -> Task<obj>) : obj = jsNative

    [<Emit("$0.connect($1)")>]
    let private connect (server: obj) (transport: obj) : JS.Promise<unit> = jsNative

    [<Emit("import.meta.url")>]
    let private moduleUrl () : string = jsNative

    [<Emit("process.argv[1] || ''")>]
    let private entryArgument () : string = jsNative

    [<Emit("$0.catch($1)")>]
    let private catchPromise (promise: JS.Promise<unit>) (onError: obj -> unit) : unit = jsNative

    [<Emit("console.error($0)")>]
    let private consoleError (line: string) : unit = jsNative

    [<Emit("Date.now()")>]
    let private nowMs () : float = jsNative

    [<Emit("process.exit(1)")>]
    let private exitFailure () : unit = jsNative

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private serverInstructions =
        "Sphinx is kernel-controlled.\n"
        + "Start an inquiry once.\n"
        + "When a result has status=\"yield\", call exactly the tool named by nextTool.\n"
        + "Do not choose another inquiry phase yourself.\n"
        + "Candidate proposals are not evidence.\n"
        + "Investigation evidence must be explicit and sourced.\n"
        + "When status=\"answered\", stop calling Sphinx for that inquiry.\n"
        + "Use status if continuation is uncertain."

    let private textContent (text: string) =
        createObj [ "type" ==> "text"; "text" ==> text ]

    let private successResult (summary: string) (payload: obj) =
        createObj [ "content" ==> [| textContent summary |]; "structuredContent" ==> payload ]

    let private errorResult (tool: string) (view: McpContract.ErrorView) =
        createObj
            [ "content" ==> [| textContent (McpContract.summarizeError view) |]
              "isError" ==> true
              "_meta"
              ==> createObj [ "tool" ==> tool; "error" ==> McpContract.errorObject view ] ]

    let private handlePrefix (handle: string option) =
        match handle with
        | Some value when value.Length > 8 -> value.Substring(0, 8)
        | Some value -> value
        | None -> "-"

    let private revisionText (revision: int option) =
        revision |> Option.map string |> Option.defaultValue "-"

    let private logSuccess (tool: string) (handle: string) (status: string) (revision: int) (startedMs: float) =
        consoleError (
            sprintf
                "[sphinx-mcp] tool=%s handle=%s status=%s revision=%d durationMs=%d"
                tool
                (handlePrefix (Some handle))
                status
                revision
                (int (nowMs () - startedMs))
        )

    let private logError (tool: string) (view: McpContract.ErrorView) (startedMs: float) =
        consoleError (
            sprintf
                "[sphinx-mcp] tool=%s handle=%s error=%s revision=%s durationMs=%d"
                tool
                (handlePrefix view.Handle)
                view.Code
                (revisionText view.Revision)
                (int (nowMs () - startedMs))
        )

    let private outcomeStatusAndRevision (success: SessionSuccess) =
        match success.Result with
        | InquiryResult.Yield _ -> "yield", success.State.Revision
        | InquiryResult.Answered answer -> "answered", answer.Revision
        | InquiryResult.Error _ -> "error", success.State.Revision

    let private resumeResultOf (tool: string) (startedMs: float) (outcome: SessionOutcome) : obj =
        match outcome with
        | SessionOutcome.Success success ->
            let status, revision = outcomeStatusAndRevision success
            logSuccess tool success.Handle status revision startedMs
            successResult (McpContract.summarizeSuccess success) (McpContract.successPayload success)
        | SessionOutcome.Failure failure ->
            let view = McpContract.failureView failure
            logError tool view startedMs
            errorResult tool view

    let private optionalHandle (value: obj) : string option =
        if isNullish value then None else Some(unbox<string> value)

    let private lookupFailure
        (tool: string)
        (startedMs: float)
        (outcome: LookupOutcome<'Value>)
        (ok: string -> 'Value -> obj)
        : obj =
        match outcome with
        | LookupOutcome.Found(handle, value) -> ok handle value
        | LookupOutcome.MissingHandle ->
            let view =
                McpContract.failureView
                    { Handle = None
                      State = None
                      Failure = SessionFailure.MissingHandle }

            logError tool view startedMs
            errorResult tool view
        | LookupOutcome.UnknownHandle handle ->
            let view =
                McpContract.failureView
                    { Handle = Some handle
                      State = None
                      Failure = SessionFailure.UnknownHandle }

            logError tool view startedMs
            errorResult tool view

    let private startOutcomeResult (startedMs: float) (outcome: StartOutcome) : obj =
        match outcome with
        | StartOutcome.Started(handle, state, result) ->
            let success: SessionSuccess =
                { Handle = handle
                  State = state
                  Result = result }

            let status, revision = outcomeStatusAndRevision success
            logSuccess McpContract.toolStart handle status revision startedMs
            successResult (McpContract.summarizeSuccess success) (McpContract.successPayload success)
        | StartOutcome.Rejected message ->
            let view = McpContract.startQuestionRequiredView message
            logError McpContract.toolStart view startedMs
            errorResult McpContract.toolStart view

    let private startHandler (store: SessionStore) (args: obj) : obj =
        startOutcomeResult (nowMs ()) (store.StartTyped(unbox<string> args?question))

    let private observationHandler
        (tool: string)
        (decode: obj -> Result<Observation, string>)
        (store: SessionStore)
        (args: obj)
        : obj =
        let startedMs = nowMs ()
        let handle = unbox<string> args?handle

        match decode args with
        | Error message ->
            let view = McpContract.invalidObservationView (optionalHandle args?handle) message
            logError tool view startedMs
            errorResult tool view
        | Ok observation -> store.ResumeObservation(handle, observation) |> resumeResultOf tool startedMs

    let private resumeLegacyHandler (store: SessionStore) (args: obj) : obj =
        let startedMs = nowMs ()
        let handle = unbox<string> args?handle

        match ObservationCodec.decode args?observation with
        | Error message ->
            let view = McpContract.invalidObservationView (optionalHandle args?handle) message
            logError McpContract.toolResumeLegacy view startedMs
            errorResult McpContract.toolResumeLegacy view
        | Ok observation ->
            store.ResumeObservation(handle, observation)
            |> resumeResultOf McpContract.toolResumeLegacy startedMs

    let private statusHandler (store: SessionStore) (args: obj) : obj =
        let startedMs = nowMs ()

        lookupFailure McpContract.toolStatus startedMs (store.Status(unbox<string> args?handle)) (fun handle status ->
            let revision =
                match status with
                | SessionStatus.Active state -> state.Revision
                | SessionStatus.Answered(answer, _) -> answer.Revision

            let statusName =
                match status with
                | SessionStatus.Active _ -> "active"
                | SessionStatus.Answered _ -> "answered"

            logSuccess McpContract.toolStatus handle statusName revision startedMs
            successResult (McpContract.summarizeStatus handle status) (McpContract.statusPayload handle status))

    let private cancelHandler (store: SessionStore) (args: obj) : obj =
        let startedMs = nowMs ()

        lookupFailure McpContract.toolCancel startedMs (store.Cancel(unbox<string> args?handle)) (fun handle () ->
            consoleError (
                sprintf
                    "[sphinx-mcp] tool=%s handle=%s status=cancelled durationMs=%d"
                    McpContract.toolCancel
                    (handlePrefix (Some handle))
                    (int (nowMs () - startedMs))
            )

            successResult (McpContract.summarizeCancel ()) (McpContract.cancelPayload handle))

    let private textField (args: obj) (name: string) : string =
        let raw: obj = emitJsExpr (args, name) "$0[$1]"
        if isNullish raw then "" else string raw

    let private optField (args: obj) (name: string) : obj = emitJsExpr (args, name) "$0[$1]"

    let private nullIfMissing (value: obj) : obj = if isNullish value then null else value

    let private inquiryFaultView (fault: GecInquiry.InquiryFault) : McpContract.ErrorView =
        match fault with
        | GecInquiry.InquiryFault.RevisionConflict(inquiryId, current) ->
            { Code = GecInquiry.faultCode fault
              Message = GecInquiry.faultMessage fault
              Recoverable = true
              Retryable = false
              NextAction = "Call sphinx_inquiry_status and resubmit at the current revision."
              Handle = Some inquiryId
              Revision = Some current
              ExpectedTool = None }
        | GecInquiry.InquiryFault.UnknownInquiry inquiryId
        | GecInquiry.InquiryFault.InquiryCancelled inquiryId ->
            { Code = GecInquiry.faultCode fault
              Message = GecInquiry.faultMessage fault
              Recoverable = false
              Retryable = false
              NextAction = "Start a new generic inquiry with sphinx_inquiry_start."
              Handle = Some inquiryId
              Revision = None
              ExpectedTool = None }

    let private genericStartedText (entry: GecInquiry.GecInquiryEntry) : string =
        sprintf "Sphinx generic inquiry started.\nInquiryId: %s\nRevision: %d" entry.InquiryId entry.InquiryRevision

    let private genericSubmitText (entry: GecInquiry.GecInquiryEntry) (accepted: int) : string =
        sprintf
            "Sphinx generic work submitted.\nInquiryId: %s\nRevision: %d\nAccepted: %d"
            entry.InquiryId
            entry.InquiryRevision
            accepted

    let private genericActiveText (verb: string) (entry: GecInquiry.GecInquiryEntry) : string =
        sprintf "Sphinx generic inquiry %s.\nInquiryId: %s\nRevision: %d" verb entry.InquiryId entry.InquiryRevision

    let private genericCancelledText (entry: GecInquiry.GecInquiryEntry) : string =
        sprintf "Sphinx generic inquiry cancelled.\nInquiryId: %s\nRevision: %d" entry.InquiryId entry.InquiryRevision

    let private inquiryStartHandler (registry: GecInquiry.Registry) (args: obj) : obj =
        let startedMs = nowMs ()
        let question = textField args "question"

        if System.String.IsNullOrWhiteSpace question then
            let view = McpContract.genericStartQuestionRequiredView "question required"
            logError GecInquiry.toolGenericStart view startedMs
            errorResult GecInquiry.toolGenericStart view
        else
            let entry: GecInquiry.GecInquiryEntry =
                registry.Start(
                    question,
                    textField args "profile",
                    nullIfMissing (optField args "plugins"),
                    textField args "executionMode",
                    nullIfMissing (optField args "budget")
                )

            logSuccess GecInquiry.toolGenericStart entry.InquiryId "active" entry.InquiryRevision startedMs
            successResult (genericStartedText entry) (GecInquiry.entryView entry)

    let private inquirySubmitHandler (registry: GecInquiry.Registry) (args: obj) : obj =
        let startedMs = nowMs ()
        let inquiryId = textField args "inquiryId"
        let expectedRaw: obj = optField args "expectedRevision"

        let expected: int = if isNullish expectedRaw then -1 else unbox<int> expectedRaw

        let resultsRaw: obj = optField args "results"

        let results: obj list =
            if isNullish resultsRaw then
                []
            else
                unbox<obj array> resultsRaw |> Array.toList

        match registry.Submit(inquiryId, expected, results) with
        | Ok(entry: GecInquiry.GecInquiryEntry) ->
            logSuccess GecInquiry.toolGenericSubmit entry.InquiryId "active" entry.InquiryRevision startedMs
            successResult (genericSubmitText entry results.Length) (GecInquiry.submitView entry results.Length)
        | Error(fault: GecInquiry.InquiryFault) ->
            let view: McpContract.ErrorView = inquiryFaultView fault
            logError GecInquiry.toolGenericSubmit view startedMs
            errorResult GecInquiry.toolGenericSubmit view

    let private inquiryLookupError (tool: string) (startedMs: float) (fault: GecInquiry.InquiryFault) : obj =
        let view: McpContract.ErrorView = inquiryFaultView fault
        logError tool view startedMs
        errorResult tool view

    let private cancelledOrActiveResult
        (tool: string)
        (verb: string)
        (view: GecInquiry.GecInquiryEntry -> obj)
        (entry: GecInquiry.GecInquiryEntry)
        (startedMs: float)
        : obj =
        if entry.InquiryCancelled then
            inquiryLookupError tool startedMs (GecInquiry.InquiryFault.InquiryCancelled entry.InquiryId)
        else
            logSuccess tool entry.InquiryId "active" entry.InquiryRevision startedMs
            successResult (genericActiveText verb entry) (view entry)

    let private inquiryStatusHandler (registry: GecInquiry.Registry) (args: obj) : obj =
        let startedMs = nowMs ()
        let inquiryId = textField args "inquiryId"

        match registry.TryFind inquiryId with
        | Some(entry: GecInquiry.GecInquiryEntry) ->
            cancelledOrActiveResult GecInquiry.toolGenericStatus "active" GecInquiry.entryView entry startedMs
        | None ->
            inquiryLookupError GecInquiry.toolGenericStatus startedMs (GecInquiry.InquiryFault.UnknownInquiry inquiryId)

    let private inquiryExportHandler (registry: GecInquiry.Registry) (args: obj) : obj =
        let startedMs = nowMs ()
        let inquiryId = textField args "inquiryId"

        match registry.TryFind inquiryId with
        | Some(entry: GecInquiry.GecInquiryEntry) ->
            cancelledOrActiveResult GecInquiry.toolGenericExport "exported" GecInquiry.exportView entry startedMs
        | None ->
            inquiryLookupError GecInquiry.toolGenericExport startedMs (GecInquiry.InquiryFault.UnknownInquiry inquiryId)

    let private inquiryCancelHandler (registry: GecInquiry.Registry) (args: obj) : obj =
        let startedMs = nowMs ()
        let inquiryId = textField args "inquiryId"

        match registry.Cancel inquiryId with
        | Ok(entry: GecInquiry.GecInquiryEntry) ->
            logSuccess GecInquiry.toolGenericCancel entry.InquiryId "cancelled" entry.InquiryRevision startedMs
            successResult (genericCancelledText entry) (GecInquiry.cancelView entry)
        | Error(fault: GecInquiry.InquiryFault) ->
            let view: McpContract.ErrorView = inquiryFaultView fault
            logError GecInquiry.toolGenericCancel view startedMs
            errorResult GecInquiry.toolGenericCancel view

    let private appendOutcomeReason (appendError: AppendError) : string =
        match appendError with
        | AppendError.StorageInvalid invalid -> sprintf "durable storage rejected the observation: %A" invalid
        | AppendError.SemanticCut cut -> sprintf "durable semantic cut by rule %s: %s" cut.Rule cut.Reason
        | AppendError.AppendFailed reason -> reason

    // WHAT[EPI-019]: generic durable-append failure. Memory is never advanced
    // before the envelope lands, so the caller retries the identical call.
    let private genericAppendFailedView (tool: string) (inquiryId: string) (reason: string) : McpContract.ErrorView =
        { Code = "durable-append-failed"
          Message =
            sprintf
                "generic inquiry %s was not made durable (%s); memory is unchanged, retry the same call"
                inquiryId
                reason
          Recoverable = true
          Retryable = false
          NextAction = "Retry the same call; nothing advanced."
          Handle = Some inquiryId
          Revision = None
          ExpectedTool = None }

    let private appendGenericEnvelope
        (events: IEventStore)
        (tool: string)
        (envelope: EventEnvelope)
        (inquiryId: string)
        (startedMs: float)
        : Task<Result<unit, McpContract.ErrorView>> =
        task {
            match! events.Append [ envelope ] with
            | Ok _ -> return Ok()
            | Error appendError ->
                let view = genericAppendFailedView tool inquiryId (appendOutcomeReason appendError)
                logError tool view startedMs
                return Error view
        }

    let private startEntryDurable
        (registry: GecInquiry.Registry)
        (events: IEventStore)
        (args: obj)
        (question: string)
        (startedMs: float)
        : Task<obj> =
        task {
            let entry: GecInquiry.GecInquiryEntry =
                GecInquiry.BuildStart(
                    question,
                    textField args "profile",
                    nullIfMissing (optField args "plugins"),
                    textField args "executionMode",
                    nullIfMissing (optField args "budget")
                )

            let! appended =
                appendGenericEnvelope
                    events
                    GecInquiry.toolGenericStart
                    (GenericDurability.encodeStarted entry)
                    entry.InquiryId
                    startedMs

            match appended with
            | Error view -> return errorResult GecInquiry.toolGenericStart view
            | Ok() ->
                registry.Restore entry
                logSuccess GecInquiry.toolGenericStart entry.InquiryId "active" entry.InquiryRevision startedMs
                return successResult (genericStartedText entry) (GecInquiry.entryView entry)
        }

    let private inquiryStartDurableHandler
        (registry: GecInquiry.Registry)
        (events: IEventStore)
        (args: obj)
        : Task<obj> =
        task {
            let startedMs = nowMs ()
            let question = textField args "question"

            if System.String.IsNullOrWhiteSpace question then
                let view = McpContract.genericStartQuestionRequiredView "question required"
                logError GecInquiry.toolGenericStart view startedMs
                return errorResult GecInquiry.toolGenericStart view
            else
                return! startEntryDurable registry events args question startedMs
        }

    let private storeGenericSubmit
        (registry: GecInquiry.Registry)
        (events: IEventStore)
        (entry: GecInquiry.GecInquiryEntry)
        (expected: int)
        (results: obj list)
        (startedMs: float)
        : Task<obj> =
        task {
            let! appended =
                appendGenericEnvelope
                    events
                    GecInquiry.toolGenericSubmit
                    (GenericDurability.encodeSubmitted entry expected results)
                    entry.InquiryId
                    startedMs

            match appended with
            | Error view -> return errorResult GecInquiry.toolGenericSubmit view
            | Ok() ->
                registry.Restore entry
                logSuccess GecInquiry.toolGenericSubmit entry.InquiryId "active" entry.InquiryRevision startedMs

                return
                    successResult (genericSubmitText entry results.Length) (GecInquiry.submitView entry results.Length)
        }

    let private submitDecidedDurable
        (registry: GecInquiry.Registry)
        (events: IEventStore)
        (entry: GecInquiry.GecInquiryEntry)
        (expected: int)
        (results: obj list)
        (startedMs: float)
        : Task<obj> =
        task {
            match GecInquiry.DecideSubmit(entry, expected, results) with
            | Error fault ->
                let view: McpContract.ErrorView = inquiryFaultView fault
                logError GecInquiry.toolGenericSubmit view startedMs
                return errorResult GecInquiry.toolGenericSubmit view
            | Ok next -> return! storeGenericSubmit registry events next expected results startedMs
        }

    let private inquirySubmitDurableHandler
        (registry: GecInquiry.Registry)
        (events: IEventStore)
        (args: obj)
        : Task<obj> =
        task {
            let startedMs = nowMs ()
            let inquiryId = textField args "inquiryId"
            let expectedRaw: obj = optField args "expectedRevision"

            let expected: int = if isNullish expectedRaw then -1 else unbox<int> expectedRaw

            let resultsRaw: obj = optField args "results"

            let results: obj list =
                if isNullish resultsRaw then
                    []
                else
                    unbox<obj array> resultsRaw |> Array.toList

            match registry.TryFind inquiryId with
            | None ->
                return
                    inquiryLookupError
                        GecInquiry.toolGenericSubmit
                        startedMs
                        (GecInquiry.InquiryFault.UnknownInquiry inquiryId)
            | Some(entry: GecInquiry.GecInquiryEntry) ->
                return! submitDecidedDurable registry events entry expected results startedMs
        }

    let private storeGenericCancel
        (registry: GecInquiry.Registry)
        (events: IEventStore)
        (entry: GecInquiry.GecInquiryEntry)
        (startedMs: float)
        : Task<obj> =
        task {
            let! appended =
                appendGenericEnvelope
                    events
                    GecInquiry.toolGenericCancel
                    (GenericDurability.encodeCancelled entry)
                    entry.InquiryId
                    startedMs

            match appended with
            | Error view -> return errorResult GecInquiry.toolGenericCancel view
            | Ok() ->
                registry.Restore entry
                logSuccess GecInquiry.toolGenericCancel entry.InquiryId "cancelled" entry.InquiryRevision startedMs
                return successResult (genericCancelledText entry) (GecInquiry.cancelView entry)
        }

    let private cancelDecidedDurable
        (registry: GecInquiry.Registry)
        (events: IEventStore)
        (entry: GecInquiry.GecInquiryEntry)
        (startedMs: float)
        : Task<obj> =
        task {
            match GecInquiry.DecideCancel entry with
            | Error fault ->
                let view: McpContract.ErrorView = inquiryFaultView fault
                logError GecInquiry.toolGenericCancel view startedMs
                return errorResult GecInquiry.toolGenericCancel view
            | Ok next -> return! storeGenericCancel registry events next startedMs
        }

    let private inquiryCancelDurableHandler
        (registry: GecInquiry.Registry)
        (events: IEventStore)
        (args: obj)
        : Task<obj> =
        task {
            let startedMs = nowMs ()
            let inquiryId = textField args "inquiryId"

            match registry.TryFind inquiryId with
            | None ->
                return
                    inquiryLookupError
                        GecInquiry.toolGenericCancel
                        startedMs
                        (GecInquiry.InquiryFault.UnknownInquiry inquiryId)
            | Some(entry: GecInquiry.GecInquiryEntry) -> return! cancelDecidedDurable registry events entry startedMs
        }

    let private sphinxGenericCurrentKey = "SphinxGeneric"

    let private restoreGenericOrThrow (current: obj) : GecInquiry.Registry =
        match GenericDurability.restore (unbox<GenericIntegrator.SphinxGenericCurrent> current) with
        | Ok registry -> registry
        | Error message -> failwith message

    let private bootGenericOrThrow (events: IEventStore) : GecInquiry.Registry =
        match events.TryCurrent sphinxGenericCurrentKey with
        | None -> GecInquiry.Registry()
        | Some current -> restoreGenericOrThrow current

    // WHAT[EPI-030]: SPHINX_COMMON_DIR selects the durable workspace. Missing
    // or blank keeps the legacy in-memory server with no store contact.
    let private sphinxCommonDirEnv = "SPHINX_COMMON_DIR"

    // WHAT[EPI-030]: sanctioned Current slot written by the Sphinx rule.
    let private sphinxCurrentKey = "Sphinx"

    let private readCommonDir () : string option =
        match Environment.GetEnvironmentVariable sphinxCommonDirEnv with
        | null
        | "" -> None
        | value when String.IsNullOrWhiteSpace value -> None
        | value -> Some value

    // WHAT[EPI-030]: durable identity mirrors LegacyDurability: one stream per
    // handle, deterministic handle:revision ids chained by causal parents, and
    // the accepted args inlined as canonical JSON for replay.
    let private legacyEnvelopeOf
        (handle: string)
        (tool: string)
        (argsJson: string)
        (revision: int)
        (question: string)
        : EventEnvelope =
        EventEnvelope.normalize
            { EventId = Wanxiangshu.Foundation.Identity.EventId.create (LegacyDurability.envelopeId handle revision)
              StreamId = EventStreamId.create (LegacyDurability.streamFor handle)
              EventType = LegacyDurability.observationType
              Parents =
                if revision <= 0 then
                    []
                else
                    [ Wanxiangshu.Foundation.Identity.EventId.create (LegacyDurability.envelopeId handle (revision - 1)) ]
              Payload =
                Encode.object
                    [ "handle", Encode.string handle
                      "tool", Encode.string tool
                      "args_json", Encode.string argsJson
                      "revision", Encode.int revision
                      "question", Encode.string question ]
              PayloadRefs = [] }

    // WHAT[EPI-030]: durable-append failures are typed MCP errors, never
    // silent. The session already advanced in memory, so the revision can never
    // be appended again; only in-memory work continues.
    let private durableAppendFailedView (handle: string) (revision: int) (reason: string) : McpContract.ErrorView =
        { Code = "durable-append-failed"
          Message = reason
          Recoverable = true
          Retryable = false
          NextAction =
            "The inquiry advanced in memory but was not made durable, so it will not survive restart. Continue in memory, or start a new inquiry for restart-safe work."
          Handle = Some handle
          Revision = Some revision
          ExpectedTool = None }

    let private appendLegacyObservation
        (events: IEventStore)
        (tool: string)
        (handle: string)
        (args: obj)
        (revision: int)
        (startedMs: float)
        : Task<Result<unit, McpContract.ErrorView>> =
        task {
            let question =
                if tool = McpContract.toolStart then
                    textField args "question"
                else
                    ""

            let envelope =
                legacyEnvelopeOf handle tool (CoreHash.canonical args) revision question

            match! events.Append [ envelope ] with
            | Ok _ -> return Ok()
            | Error appendError ->
                let view = durableAppendFailedView handle revision (appendOutcomeReason appendError)
                logError tool view startedMs
                return Error view
        }

    let private decodeDurableObservation
        (tool: string)
        (decode: obj -> Result<Observation, string>)
        (args: obj)
        (startedMs: float)
        : Result<Observation, McpContract.ErrorView> =
        decode args
        |> Result.mapError (fun message ->
            let view = McpContract.invalidObservationView (optionalHandle args?handle) message
            logError tool view startedMs
            view)

    let private resumeDurableObservation
        (tool: string)
        (sessions: SessionStore)
        (handle: string)
        (observation: Observation)
        (startedMs: float)
        : Result<SessionSuccess, McpContract.ErrorView> =
        match sessions.ResumeObservation(handle, observation) with
        | SessionOutcome.Success success -> Ok success
        | SessionOutcome.Failure failure ->
            let view = McpContract.failureView failure
            logError tool view startedMs
            Error view

    let private renderDurableOutcome (tool: string) (outcome: Result<obj, McpContract.ErrorView>) : obj =
        match outcome with
        | Ok rendered -> rendered
        | Error view -> errorResult tool view

    let private startDurableHandler (sessions: SessionStore) (events: IEventStore) (args: obj) : Task<obj> =
        task {
            let startedMs = nowMs ()

            match sessions.StartTyped(unbox<string> args?question) with
            | StartOutcome.Rejected _ as rejected -> return startOutcomeResult startedMs rejected
            | StartOutcome.Started(handle, state, _) as started ->
                let! outcome =
                    taskResult {
                        do! appendLegacyObservation events McpContract.toolStart handle args state.Revision startedMs
                        return startOutcomeResult startedMs started
                    }

                return renderDurableOutcome McpContract.toolStart outcome
        }

    let private observationDurableHandler
        (tool: string)
        (decode: obj -> Result<Observation, string>)
        (sessions: SessionStore)
        (events: IEventStore)
        (args: obj)
        : Task<obj> =
        task {
            let startedMs = nowMs ()
            let handle = unbox<string> args?handle

            let! outcome =
                taskResult {
                    let! observation = decodeDurableObservation tool decode args startedMs
                    let! success = resumeDurableObservation tool sessions handle observation startedMs
                    do! appendLegacyObservation events tool handle args success.State.Revision startedMs
                    return resumeResultOf tool startedMs (SessionOutcome.Success success)
                }

            return renderDurableOutcome tool outcome
        }

    let private replayToolOfObservation (observation: Observation) : string =
        match observation with
        | SemanticAssessmentObservation _ -> McpContract.toolAssess
        | CandidatesObservation _ -> McpContract.toolPropose
        | InvestigationObservation _ -> McpContract.toolInvestigate
        | SynthesisObservation _ -> McpContract.toolSynthesize

    let private decodeLegacyDurableObservation
        (args: obj)
        (startedMs: float)
        : Result<Observation, McpContract.ErrorView> =
        ObservationCodec.decode args?observation
        |> Result.mapError (fun message ->
            let view = McpContract.invalidObservationView (optionalHandle args?handle) message
            logError McpContract.toolResumeLegacy view startedMs
            view)

    let private resumeLegacyDurableHandler (sessions: SessionStore) (events: IEventStore) (args: obj) : Task<obj> =
        task {
            let startedMs = nowMs ()
            let handle = unbox<string> args?handle

            let! outcome =
                taskResult {
                    let! observation = decodeLegacyDurableObservation args startedMs

                    let! success =
                        resumeDurableObservation McpContract.toolResumeLegacy sessions handle observation startedMs

                    do!
                        appendLegacyObservation
                            events
                            (replayToolOfObservation observation)
                            handle
                            args?observation
                            success.State.Revision
                            startedMs

                    return resumeResultOf McpContract.toolResumeLegacy startedMs (SessionOutcome.Success success)
                }

            return renderDurableOutcome McpContract.toolResumeLegacy outcome
        }

    let private candidateSchema =
        createObj
            [ "method" ==> zBareString ()
              "question" ==> zBareString ()
              "semanticKey" ==> zBareString ()
              "dependencyKey" ==> zOptional (zBareString ())
              "expectedRootGain" ==> zOptional (zNumber ())
              "gatewayGain" ==> zOptional (zNumber ())
              "cost" ==> zOptional (zNumber ())
              "provenance" ==> zOptional (zBareStringArray ()) ]

    let private register
        (server: obj)
        (name: string)
        (title: string)
        (description: string)
        (inputSchema: obj)
        (handler: obj -> obj)
        =
        registerTool
            server
            name
            (createObj
                [ "title" ==> title
                  "description" ==> description
                  "inputSchema" ==> inputSchema ])
            (unaryHandler handler)
        |> ignore

    let private registerAsync
        (server: obj)
        (name: string)
        (title: string)
        (description: string)
        (inputSchema: obj)
        (handler: obj -> Task<obj>)
        =
        registerTool
            server
            name
            (createObj
                [ "title" ==> title
                  "description" ==> description
                  "inputSchema" ==> inputSchema ])
            (asyncUnaryHandler handler)
        |> ignore

    // WHAT[EPI-030]: one registration source for both servers. None keeps the
    // legacy sync handlers; Some store swaps the six mutating tools to
    // append-after-accept handlers with identical titles and schemas.
    let private buildServer (sessions: SessionStore) (durable: IEventStore option) : obj =
        let server =
            construct
                mcpServerConstructor
                (createObj [ "name" ==> SphinxMcp.serverName; "version" ==> PackageMetadata.version () ])
                (createObj [ "instructions" ==> serverInstructions ])

        let inquiries =
            match durable with
            | None -> GecInquiry.Registry()
            | Some events -> bootGenericOrThrow events

        let registerDurable
            (tool: string)
            (title: string)
            (description: string)
            (inputSchema: obj)
            (sync: obj -> obj)
            (async: IEventStore -> obj -> Task<obj>)
            =
            match durable with
            | None -> register server tool title description inputSchema sync
            | Some events -> registerAsync server tool title description inputSchema (async events)

        registerDurable
            McpContract.toolStart
            "Start Sphinx inquiry"
            "Start one kernel-controlled Sphinx inquiry. If the result yields, follow nextTool; do not choose another phase yourself."
            (createObj [ "question" ==> zString "Root question" ])
            (startHandler sessions)
            (startDurableHandler sessions)

        registerDurable
            McpContract.toolAssess
            "Assess question semantics"
            "Answer the pending SemanticAssessmentRequest. forms maps QuestionForm (Why/How/What/Who/Where/When/Which/Polar/Other) to belief mass; facets, targets and intents are optional. An empty forms map abstains (no belief mass) and still advances the inquiry."
            (createObj
                [ "handle" ==> zString "Opaque inquiry handle returned by start"
                  "forms" ==> zNumberRecord "QuestionForm → belief mass"
                  "facets" ==> zOptional (zNumberRecord "Facet → applicability")
                  "targets" ==> zOptional (zStringArray "Target terms")
                  "intents" ==> zOptional (zStringArray "Intent terms") ])
            (observationHandler McpContract.toolAssess ObservationCodec.decodeSemanticAssessment sessions)
            (observationDurableHandler McpContract.toolAssess ObservationCodec.decodeSemanticAssessment sessions)

        registerDurable
            McpContract.toolPropose
            "Propose investigation candidates"
            "Answer the pending GenerateCandidatesRequest with candidate investigation proposals. Proposals are not evidence; the kernel decides which action is investigated."
            (createObj
                [ "handle" ==> zString "Opaque inquiry handle returned by start"
                  "items"
                  ==> zObjectArray
                          candidateSchema
                          "Candidate proposals: method, question, semanticKey; optional dependencyKey, expectedRootGain, gatewayGain, cost, provenance" ])
            (observationHandler McpContract.toolPropose ObservationCodec.decodeCandidates sessions)
            (observationDurableHandler McpContract.toolPropose ObservationCodec.decodeCandidates sessions)

        registerDurable
            McpContract.toolInvestigate
            "Investigate the selected action"
            "Answer the pending InvestigateRequest. actionKey must be copied exactly from the current InvestigateRequest.action.id. Findings, evidence, hypotheses, candidates and semanticAssessment are optional collections; each evidence entry is an object with semanticKey, proposition, source object carrying id, and dependencyKey. A bare-string source is rejected as INVALID_OBSERVATION without advancing the revision."
            (createObj
                [ "handle" ==> zString "Opaque inquiry handle returned by start"
                  "actionKey" ==> zString "Copy exactly from InvestigateRequest.action.id"
                  "semanticAssessment"
                  ==> zOptional (zRecord "Optional control-only semantic reassessment")
                  "findings" ==> zOptional (zRecordArray "Findings with semanticKey and text")
                  "evidence"
                  ==> zOptional (zRecordArray "Evidence with semanticKey, proposition, source, dependencyKey")
                  "hypotheses"
                  ==> zOptional (zRecordArray "Hypotheses with semanticKey and label")
                  "candidates" ==> zOptional (zRecordArray "Follow-up candidate proposals") ])
            (observationHandler McpContract.toolInvestigate ObservationCodec.decodeInvestigation sessions)
            (observationDurableHandler McpContract.toolInvestigate ObservationCodec.decodeInvestigation sessions)

        registerDurable
            McpContract.toolSynthesize
            "Synthesize findings"
            "Answer the pending SynthesizeRequest. Synthesis organizes existing findings; it does not create evidence. findingKeys must reference known findings."
            (createObj
                [ "handle" ==> zString "Opaque inquiry handle returned by start"
                  "text" ==> zString "Synthesis text"
                  "findingKeys"
                  ==> zOptional (zStringArray "Known finding keys organized by this synthesis")
                  "uncertainties" ==> zOptional (zStringArray "Explicit uncertainties") ])
            (observationHandler McpContract.toolSynthesize ObservationCodec.decodeSynthesis sessions)
            (observationDurableHandler McpContract.toolSynthesize ObservationCodec.decodeSynthesis sessions)

        register
            server
            McpContract.toolStatus
            "Inquiry status"
            "Read the current status of one inquiry: active with nextTool and pending request, or answered with the canonical answer."
            (createObj [ "handle" ==> zString "Opaque inquiry handle returned by start" ])
            (statusHandler sessions)

        register
            server
            McpContract.toolCancel
            "Cancel inquiry"
            "Cancel one inquiry and release its handle. The handle becomes unknown immediately."
            (createObj [ "handle" ==> zString "Opaque inquiry handle returned by start" ])
            (cancelHandler sessions)

        registerDurable
            McpContract.toolResumeLegacy
            "Resume Sphinx inquiry (legacy)"
            "Legacy compatibility tool. Prefer the phase-specific tool named by nextTool. Continues the same inquiry with a raw observation object carrying an explicit type field."
            (createObj
                [ "handle" ==> zString "Opaque inquiry handle returned by start"
                  "observation" ==> zRecord "Raw observation with explicit type field" ])
            (resumeLegacyHandler sessions)
            (resumeLegacyDurableHandler sessions)

        registerDurable
            GecInquiry.toolGenericStart
            "Start generic inquiry"
            "Start one schema-only generic inquiry and return its iq_ handle at revision 0. The host records revisions only; it never returns refiner, stop or answer verdicts."
            (createObj
                [ "question" ==> zString "Root question"
                  "profile" ==> zOptional (zString "Profile name")
                  "plugins" ==> zOptional (zAnyArray "Plugin descriptors")
                  "executionMode" ==> zOptional (zString "Execution mode")
                  "budget" ==> zOptional (zRecord "Budget by currency") ])
            (inquiryStartHandler inquiries)
            (inquiryStartDurableHandler inquiries)

        registerDurable
            GecInquiry.toolGenericSubmit
            "Submit generic work results"
            "Submit worker results at an expected revision. A stale expectedRevision fails with REVISION_CONFLICT and advances nothing."
            (createObj
                [ "inquiryId" ==> zString "Generic inquiry id returned by sphinx_inquiry_start"
                  "expectedRevision" ==> zNumber ()
                  "results" ==> zAnyArray "Work results applied at the next revision" ])
            (inquirySubmitHandler inquiries)
            (inquirySubmitDurableHandler inquiries)

        register
            server
            GecInquiry.toolGenericStatus
            "Generic inquiry status"
            "Read one generic inquiry revision and liveness without judging it."
            (createObj [ "inquiryId" ==> zString "Generic inquiry id returned by sphinx_inquiry_start" ])
            (inquiryStatusHandler inquiries)

        register
            server
            GecInquiry.toolGenericExport
            "Export generic inquiry"
            "Export the registry view of one generic inquiry: question, revision, status, budget and submitted results."
            (createObj [ "inquiryId" ==> zString "Generic inquiry id returned by sphinx_inquiry_start" ])
            (inquiryExportHandler inquiries)

        registerDurable
            GecInquiry.toolGenericCancel
            "Cancel generic inquiry"
            "Cancel one generic inquiry. Later status calls for the same id fail."
            (createObj [ "inquiryId" ==> zString "Generic inquiry id returned by sphinx_inquiry_start" ])
            (inquiryCancelHandler inquiries)
            (inquiryCancelDurableHandler inquiries)

        server

    let create (store: SessionStore) = buildServer store None

    let createDurable (sessions: SessionStore) (events: IEventStore) = buildServer sessions (Some events)

    let serveStdio (store: SessionStore) =
        let server = create store
        let transport = constructEmpty stdioTransportConstructor
        connect server transport

    let serveDurable (sessions: SessionStore) (events: IEventStore) =
        let server = createDurable sessions events
        let transport = constructEmpty stdioTransportConstructor
        connect server transport

    [<Emit("JSON.parse($0)")>]
    let private parseJson (text: string) : obj = jsNative

    let private storedRawObject (raw: obj) (handle: string) (tool: string) (argsJson: string) : Result<obj, string> =
        if String.IsNullOrWhiteSpace handle then
            Error "stored legacy raw needs a non-blank handle"
        elif String.IsNullOrWhiteSpace tool then
            Error(sprintf "stored legacy raw for %s needs a tool" handle)
        elif String.IsNullOrWhiteSpace argsJson then
            Error(sprintf "stored legacy raw for %s needs canonical args" handle)
        else
            Ok(
                createObj
                    [ "handle" ==> handle
                      "tool" ==> tool
                      "args" ==> parseJson argsJson
                      "revision" ==> optField raw "revision" ]
            )

    let private parseStoredRaw (raw: obj) : Result<obj, string> =
        try
            storedRawObject raw (textField raw "handle") (textField raw "tool") (textField raw "argsJson")
        with ex ->
            Error(sprintf "stored legacy raw is not replayable: %s" ex.Message)

    let private replayErrorText (result: obj) : string =
        let message = textField (optField result "error") "message"

        if String.IsNullOrWhiteSpace message then
            "unknown replay failure"
        else
            message

    [<Emit("$0 && $0.ok === true")>]
    let private replaySucceeded (result: obj) : bool = jsNative

    let rec private parseStoredRaws (remaining: obj list) (parsed: obj list) : Result<obj array, string> =
        match remaining with
        | [] -> Ok(List.rev parsed |> List.toArray)
        | raw :: tail ->
            parseStoredRaw raw
            |> Result.bind (fun value -> parseStoredRaws tail (value :: parsed))

    let private replayDecodedRaws (serving: SessionStore) (handle: string) (raws: obj array) : Result<unit, string> =
        let outcome = LegacyDurability.replayObservations serving raws

        if replaySucceeded outcome then
            Ok()
        else
            Error(sprintf "replay of %s failed: %s" handle (replayErrorText outcome))

    let private replayCursor
        (serving: SessionStore)
        (handle: string)
        (cursor: LegacyIntegrator.LegacyInquiryCursor)
        : Result<unit, string> =
        try
            parseStoredRaws cursor.Raws [] |> Result.bind (replayDecodedRaws serving handle)
        with ex ->
            Error(sprintf "replay of %s threw: %s" handle ex.Message)

    let rec private replayCursors
        (serving: SessionStore)
        (remaining: (string * LegacyIntegrator.LegacyInquiryCursor) list)
        : Result<SessionStore, string> =
        match remaining with
        | [] -> Ok serving
        | (handle, cursor) :: tail ->
            replayCursor serving handle cursor
            |> Result.bind (fun () -> replayCursors serving tail)

    // WHAT[EPI-030]: cross-process recovery. Every durable cursor rebuilds into
    // the serving store oldest-first before the transport connects, so server2
    // answers at the same revision server1 left behind. Any failure refuses to
    // serve rather than answering from an unrecovered store.
    let private bootFromCurrent (current: obj) : Result<SessionStore, string> =
        try
            let folded = unbox<LegacyIntegrator.SphinxLegacyCurrent> current
            let serving = SessionStore()

            let cursors =
                folded |> Map.toList |> List.sortBy (fun (_, cursor) -> cursor.Revision)

            replayCursors serving cursors
        with ex ->
            Error(sprintf "Sphinx durable current has an unexpected shape: %s" ex.Message)

    let private bootFromCurrentOption (current: obj option) : Result<SessionStore, string> =
        match current with
        | None -> Ok(SessionStore())
        | Some found -> bootFromCurrent found

    let private bootDurableSessions (events: IEventStore) : Result<SessionStore, string> =
        try
            bootFromCurrentOption (events.TryCurrent sphinxCurrentKey)
        with ex ->
            Error(sprintf "Sphinx durable boot failed: %s" ex.Message)

    let private serveBootOutcome (events: IEventStore) (booted: Result<SessionStore, string>) =
        match booted with
        | Ok serving -> serveDurable serving events
        | Error message -> failwith message

    let private serveDurableDir (commonDir: string) =
        try
            let integrator = CanonicalIntegrator.create ()

            let events =
                EventStore.createLocal commonDir (Guid.NewGuid().ToString("N")) integrator

            serveBootOutcome events (bootDurableSessions events)
        with ex ->
            consoleError (sprintf "[sphinx-mcp] durable boot failed: %s" ex.Message)
            exitFailure ()
            reraise ()

    let serveDefault () =
        match readCommonDir () with
        | None -> serveStdio Session.defaultStore
        | Some commonDir -> serveDurableDir commonDir

    let private runIfEntryPoint () =
        let argument = entryArgument ()

        if argument <> "" && resolve argument = (moduleUrl () |> fileURLToPath) then
            catchPromise (serveDefault ()) (fun error ->
                consoleError (sprintf "[sphinx-mcp] fatal: %s" (string error))
                exitFailure ())

    runIfEntryPoint ()
