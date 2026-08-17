namespace Wanxiangshu.Sphinx

open Wanxiangshu.Resources

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

    let private zString (description: string) : obj = emitJsExpr zod "$0.string().describe($1)"

    let private zNumberRecord (description: string) : obj = emitJsExpr (zod, description) "$0.record($0.string(), $0.number()).describe($1)"

    let private zRecord (description: string) : obj = emitJsExpr (zod, description) "$0.record($0.string(), $0.any()).describe($1)"

    let private zStringArray (description: string) : obj = emitJsExpr (zod, description) "$0.array($0.string()).describe($1)"

    let private zRecordArray (description: string) : obj =
        emitJsExpr (zod, description) "$0.array($0.record($0.string(), $0.any())).describe($1)"

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
              "_meta" ==> createObj [ "tool" ==> tool; "error" ==> McpContract.errorObject view ] ]

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

    let private lookupFailure (tool: string) (startedMs: float) (outcome: LookupOutcome<'Value>) (ok: string -> 'Value -> obj) : obj =
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

    let private startHandler (store: SessionStore) (args: obj) : obj =
        let startedMs = nowMs ()

        match store.StartTyped(unbox<string> args?question) with
        | StartOutcome.Started(handle, state, result) ->
            let success: SessionSuccess =
                { Handle = handle
                  State = state
                  Result = result }

            let status, revision = outcomeStatusAndRevision success
            logSuccess McpContract.toolStart handle status revision startedMs
            successResult (McpContract.summarizeSuccess success) (McpContract.successPayload success)
        | StartOutcome.Rejected message ->
            let view = McpContract.questionRequiredView message
            logError McpContract.toolStart view startedMs
            errorResult McpContract.toolStart view

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
            successResult (McpContract.summarizeStatus status) (McpContract.statusPayload handle status))

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

    let create (store: SessionStore) =
        let server =
            construct
                mcpServerConstructor
                (createObj [ "name" ==> SphinxMcp.serverName; "version" ==> PackageMetadata.version () ])
                (createObj [ "instructions" ==> serverInstructions ])

        register
            server
            McpContract.toolStart
            "Start Sphinx inquiry"
            "Start one kernel-controlled Sphinx inquiry. If the result yields, follow nextTool; do not choose another phase yourself."
            (createObj [ "question" ==> zString "Root question" ])
            (startHandler store)

        register
            server
            McpContract.toolAssess
            "Assess question semantics"
            "Answer the pending SemanticAssessmentRequest. forms maps QuestionForm (Why/How/What/Who/Where/When/Which/Polar/Other) to belief mass; facets, targets and intents are optional."
            (createObj
                [ "handle" ==> zString "Opaque inquiry handle returned by start"
                  "forms" ==> zNumberRecord "QuestionForm → belief mass"
                  "facets" ==> zOptional (zNumberRecord "Facet → applicability")
                  "targets" ==> zOptional (zStringArray "Target terms")
                  "intents" ==> zOptional (zStringArray "Intent terms") ])
            (observationHandler McpContract.toolAssess ObservationCodec.decodeSemanticAssessment store)

        register
            server
            McpContract.toolPropose
            "Propose investigation candidates"
            "Answer the pending GenerateCandidatesRequest with candidate investigation proposals. Proposals are not evidence; the kernel decides which action is investigated."
            (createObj
                [ "handle" ==> zString "Opaque inquiry handle returned by start"
                  "items"
                  ==> zObjectArray
                      candidateSchema
                      "Candidate proposals: method, question, semanticKey; optional dependencyKey, expectedRootGain, gatewayGain, cost, provenance" ])
            (observationHandler McpContract.toolPropose ObservationCodec.decodeCandidates store)

        register
            server
            McpContract.toolInvestigate
            "Investigate the selected action"
            "Answer the pending InvestigateRequest. actionKey must be copied exactly from the current InvestigateRequest.action.id. Findings, evidence, hypotheses, candidates and semanticAssessment are optional collections; evidence must be explicit and sourced."
            (createObj
                [ "handle" ==> zString "Opaque inquiry handle returned by start"
                  "actionKey" ==> zString "Copy exactly from InvestigateRequest.action.id"
                  "semanticAssessment" ==> zOptional (zRecord "Optional control-only semantic reassessment")
                  "findings" ==> zOptional (zRecordArray "Findings with semanticKey and text")
                  "evidence" ==> zOptional (zRecordArray "Evidence with semanticKey, proposition, source, dependencyKey")
                  "hypotheses" ==> zOptional (zRecordArray "Hypotheses with semanticKey and label")
                  "candidates" ==> zOptional (zRecordArray "Follow-up candidate proposals") ])
            (observationHandler McpContract.toolInvestigate ObservationCodec.decodeInvestigation store)

        register
            server
            McpContract.toolSynthesize
            "Synthesize findings"
            "Answer the pending SynthesizeRequest. Synthesis organizes existing findings; it does not create evidence. findingKeys must reference known findings."
            (createObj
                [ "handle" ==> zString "Opaque inquiry handle returned by start"
                  "text" ==> zString "Synthesis text"
                  "findingKeys" ==> zOptional (zStringArray "Known finding keys organized by this synthesis")
                  "uncertainties" ==> zOptional (zStringArray "Explicit uncertainties") ])
            (observationHandler McpContract.toolSynthesize ObservationCodec.decodeSynthesis store)

        register
            server
            McpContract.toolStatus
            "Inquiry status"
            "Read the current status of one inquiry: active with nextTool and pending request, or answered with the canonical answer."
            (createObj [ "handle" ==> zString "Opaque inquiry handle returned by start" ])
            (statusHandler store)

        register
            server
            McpContract.toolCancel
            "Cancel inquiry"
            "Cancel one inquiry and release its handle. The handle becomes unknown immediately."
            (createObj [ "handle" ==> zString "Opaque inquiry handle returned by start" ])
            (cancelHandler store)

        register
            server
            McpContract.toolResumeLegacy
            "Resume Sphinx inquiry (legacy)"
            "Legacy compatibility tool. Prefer the phase-specific tool named by nextTool. Continues the same inquiry with a raw observation object carrying an explicit type field."
            (createObj
                [ "handle" ==> zString "Opaque inquiry handle returned by start"
                  "observation" ==> zRecord "Raw observation with explicit type field" ])
            (resumeLegacyHandler store)

        server

    let serveStdio (store: SessionStore) =
        let server = create store
        let transport = constructEmpty stdioTransportConstructor
        connect server transport

    let serveDefault () = serveStdio Session.defaultStore

    let private runIfEntryPoint () =
        let argument = entryArgument ()

        if argument <> "" && resolve argument = (moduleUrl () |> fileURLToPath) then
            catchPromise (serveDefault ()) (fun error ->
                consoleError (sprintf "[sphinx-mcp] fatal: %s" (string error))
                exitFailure ())

    runIfEntryPoint ()
