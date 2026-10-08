namespace Wanxiangshu.Sphinx.V2.Hosts

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Composition
open Wanxiangshu.Sphinx.V2.Persistence
open Wanxiangshu.Sphinx.V2.Runtime
open Wanxiangshu.Sphinx.V2.Wire

/// The MCP server adapter for Sphinx v2.
///
/// WHAT[sphinx-v2-009]: the SDK lives only here. Every tool call decodes into its own
/// domain input and then goes to the one Runtime; a handler never judges an epistemic
/// stage and never assembles a state of its own.
///
/// WHAT[sphinx-v2-035]: Sphinx apiVersion is a business contract, separate from the
/// MCP protocol revision this adapter is built against. awaiting_results is the
/// content of a completed tool result, not a protocol-level request for user input.
///
/// WHAT[sphinx-v2-034]: an unconfirmed abort stays cancelling. The fold records the
/// request; only the Host can report a physical terminal.
///
/// WHAT[sphinx-v2-036]: the seven public tools. A read-only tool creates no lease,
/// calls no model and changes no business state.
module Mcp =

    [<Import("McpServer", "@modelcontextprotocol/sdk/server/mcp.js")>]
    let private mcpServerConstructor: obj = jsNative

    [<Import("StdioServerTransport", "@modelcontextprotocol/sdk/server/stdio.js")>]
    let private stdioTransportConstructor: obj = jsNative

    [<Import("z", "zod")>]
    let private zod: obj = jsNative

    [<Emit("new $0($1, $2)")>]
    let private construct (constructor: obj) (info: obj) (options: obj) : obj = jsNative

    [<Emit("new $0()")>]
    let private constructEmpty (constructor: obj) : obj = jsNative

    [<Emit("$0.string().describe($1)")>]
    let private zStringOf (z: obj) (description: string) : obj = jsNative

    /// A zod string schema carrying a human description.
    let private zString (description: string) : obj = zStringOf zod description

    [<Emit("$0.number().int().min(1).describe($1)")>]
    let private zCountOf (z: obj) (description: string) : obj = jsNative

    let private zCount (description: string) : obj = zCountOf zod description

    [<Emit("$0.array($1).describe($2)")>]
    let private zListOf (z: obj) (item: obj) (description: string) : obj = jsNative

    let private zList (item: obj) (description: string) : obj = zListOf zod item description

    [<Emit("$0.object({ id: $1, hash: $2 })")>]
    let private zSchemaRefOf (z: obj) (id: obj) (hash: obj) : obj = jsNative

    let private zSchemaRef (id: obj) (hash: obj) : obj = zSchemaRefOf zod id hash

    [<Emit("$0.object($1).passthrough()")>]
    let private zObjectOf (z: obj) (shape: obj) : obj = jsNative

    [<Emit("$0.optional()")>]
    let private zOptional (schema: obj) : obj = jsNative

    [<Emit("$0.registerTool($1, $2, $3)")>]
    let private registerTool (server: obj) (name: string) (config: obj) (handler: obj) : obj = jsNative

    [<Emit("(args) => $0(args)")>]
    let private unaryHandler (handler: obj -> Task<obj>) : obj = jsNative

    [<Emit("$0.connect($1)")>]
    let private connect (server: obj) (transport: obj) : JS.Promise<unit> = jsNative

    /// A settled promise carrying unit, used on the failure path where there is
    /// nothing left to await.
    [<Emit("Promise.resolve()")>]
    let private resolved: JS.Promise<unit> = jsNative

    [<Emit("console.error($0)")>]
    let private consoleError (line: string) : unit = jsNative

    [<Emit("process.exitCode = 1")>]
    let private markStartupFailure () : unit = jsNative

    [<Emit("JSON.parse($0)")>]
    let private parseJson (text: string) : obj = jsNative

    let private record (fields: (string * obj) list) : obj = createObj fields

    let private toolResult (isError: bool) (payload: obj) : obj =
        createObj
            [ "isError" ==> isError
              "structuredContent" ==> payload
              "content"
              ==> [| createObj [ "type" ==> "text"; "text" ==> CanonicalJson.canonicalJson payload ] |] ]

    /// The published inquiry state, read through the one canonical key.
    let private currentState (store: IEventStore) (inquiryId: InquiryId) : Result<InquiryState option, CurrentError> =
        Bind.tryInquiry store inquiryId

    let private currentRefusal (fault: CurrentError) : ToolRefusal =
        match fault with
        | CurrentError.DomainConflict _ ->
            { Code = "DOMAIN_CONFLICT"
              Path = "inquiryId"
              Message = "inquiry has multiple legitimate durable heads; no resolution command is available" }
        | CurrentError.SemanticRejected reason ->
            { Code = "PERSISTENCE_SEMANTIC_CUT"
              Path = "inquiryId"
              Message = reason }

    /// A terminal failure view. Persistence codes distinguish uncertain commits
    /// from refusals that never attempted a write.
    let private refused (refusal: ToolRefusal) : Task<obj> =
        task {
            return
                record
                    [ ("apiVersion", box Contract.apiVersion)
                      ("outcome", box "refused")
                      ("refusal",
                       record
                           [ ("code", box refusal.Code)
                             ("path", box refusal.Path)
                             ("message", box refusal.Message) ]) ]
                |> toolResult true
        }

    /// None means the inquiry is not in the durable record. It never means an empty
    /// inquiry, so a read says so instead of inventing one.
    let private unknownInquiry (inquiryId: string) : ToolRefusal =
        { Code = "UNKNOWN_INQUIRY"
          Path = "inquiryId"
          Message = sprintf "inquiry %s is not in the durable record" inquiryId }

    let private commandRefusal (fault: CommandError) : ToolRefusal =
        { Code = fault.Code
          Path = "commandId"
          Message = fault.Message }

    let private terminalToolRefusal handle (args: CancelArgs) eventId (fault: AppendError) : ToolRefusal =
        Commands.settleAppendCutUnknown handle (InquiryId.create args.InquiryId) args.CommandId eventId fault

        let view code message : ToolRefusal =
            { Code = code
              Path = "inquiryId"
              Message = message }

        let settlement code =
            let message =
                sprintf
                    "inquiry %s; command %s; event %s: %s. Reconcile the durable record before any retry."
                    args.InquiryId
                    args.CommandId
                    (Wanxiangshu.Foundation.Identity.EventId.value eventId)
                    (AppendError.describe fault)

            view code message

        match fault with
        | AppendError.StorageInvalid invalid ->
            view "PERSISTENCE_REJECTED" (sprintf "the store refused the write as invalid: %A" invalid)
        | AppendError.SemanticCut cut -> view "PERSISTENCE_REJECTED" (sprintf "semantic cut: %s" cut.Reason)
        | AppendError.AppendFailed reason -> view "PERSISTENCE_REJECTED" reason
        | AppendError.AppendNotAttempted _ -> settlement "PERSISTENCE_NOT_ATTEMPTED"
        | AppendError.CommitUnknown _ -> settlement "COMMIT_UNKNOWN"
        | AppendError.NoNewWriteReleaseFailed _ -> settlement "RELEASE_FAILED"

    let private cutRefusal (receipt: Wanxiangshu.Persistence.EventStore.AppendReceipt) : ToolRefusal =
        { Code = "PERSISTENCE_SEMANTIC_CUT"
          Path = "inquiryId"
          Message =
            receipt.Cuts
            |> List.map (fun cut -> sprintf "%s: %s" cut.Rule cut.Reason)
            |> String.concat "; " }

    let private previousHead (state: InquiryState) : Wanxiangshu.Sphinx.V2.Core.EventId option =
        state.EventHead |> Option.map (fun head -> head)

    let private runtimeResult (result: Result<obj, ToolRefusal>) : Task<obj> =
        task {
            match result with
            | Error refusal -> return! refused refusal
            | Ok payload -> return toolResult false payload
        }

    /// A durable receipt and the readable Current are different facts. A fork formed
    /// during append must not be reported as one chosen branch, or as a failed write.
    let private cancellationReceipt (store: IEventStore) (args: CancelArgs) (revision: Revision) : obj =
        let receipt =
            [ ("apiVersion", box Contract.apiVersion)
              ("outcome", box "applied")
              ("inquiryId", box args.InquiryId)
              ("revision", Encode.revision revision) ]

        let refusedCurrent (refusal: ToolRefusal) =
            record (
                receipt
                @ [ ("currentRefusal",
                     record
                         [ ("code", box refusal.Code)
                           ("path", box refusal.Path)
                           ("message", box refusal.Message) ]) ]
            )

        match currentState store (InquiryId.create args.InquiryId) with
        | Error fault -> refusedCurrent (currentRefusal fault)
        | Ok None -> refusedCurrent (unknownInquiry args.InquiryId)
        | Ok(Some current) -> record (receipt @ [ ("status", box (Encode.statusOf current)) ])

    let private appendCancellation handle (args: CancelArgs) (revision: Revision) encoded : Task<obj> =
        task {
            let store = Commands.store handle
            let! appended = store.Append [ encoded ]

            match appended with
            | Error fault -> return! refused (terminalToolRefusal handle args encoded.EventId fault)
            | Ok receipt when not (List.isEmpty receipt.Cuts) -> return! refused (cutRefusal receipt)
            | Ok _ -> return cancellationReceipt store args revision |> toolResult false
        }

    let private freshCancellation
        (handle: RuntimeHandle)
        (args: CancelArgs)
        (state: InquiryState)
        (fingerprint: string)
        : Task<obj> =
        let nextRevision = Revision.next state.Revision

        let batch =
            { SchemaVersion = "2"
              InquiryId = state.Id
              PreviousRevision = state.Revision
              PreviousHead = previousHead state
              Revision = nextRevision
              CommandId = args.CommandId
              CommandFingerprint = fingerprint
              PostStateFingerprint = None
              Events = [ InquiryEventBody.CancelRequested args.Reason ] }

        match Codec.seal HostDigest.sha256Hex (Some state) batch with
        | Error fault ->
            refused
                { Code = fault.Code
                  Path = "commandId"
                  Message = fault.Message }
        | Ok encoded -> appendCancellation handle args nextRevision encoded

    let private admittedCancellation
        (handle: RuntimeHandle)
        (args: CancelArgs)
        (state: InquiryState)
        (fingerprint: string)
        : Task<obj> =
        task {
            match
                Admission.admitCommand state args.CommandId fingerprint (InquiryCommand.CancelCommand args.Reason)
            with
            | Error fault -> return! refused (commandRefusal fault)
            | Ok(IdempotencyOutcome.Conflict message) ->
                return!
                    refused
                        { Code = "COMMAND_CONFLICT"
                          Path = "commandId"
                          Message = message }
            | Ok(IdempotencyOutcome.Replay revision) ->
                return
                    record
                        [ ("apiVersion", box Contract.apiVersion)
                          ("outcome", box "replayed")
                          ("inquiryId", box args.InquiryId)
                          ("revision", Encode.revision revision)
                          ("status", box (Encode.statusOf state)) ]
                    |> toolResult false
            | Ok(IdempotencyOutcome.Fresh _) -> return! freshCancellation handle args state fingerprint
        }

    let private cancelFromCurrent handle (args: CancelArgs) : Task<obj> =
        let store = Commands.store handle

        match currentState store (InquiryId.create args.InquiryId) with
        | Error fault -> refused (currentRefusal fault)
        | Ok None -> refused (unknownInquiry args.InquiryId)
        | Ok(Some state) ->
            let fingerprint =
                HostDigest.sha256Hex (
                    CanonicalJson.canonicalJson (
                        box
                            {| tool = Contract.toolName SphinxTool.InquiryCancel
                               inquiryId = args.InquiryId
                               reason = args.Reason |}
                    )
                )

            admittedCancellation handle args state fingerprint

    /// Requests cancellation through the canonical fold; a repeated command returns
    /// the original receipt. An unconfirmed physical abort stays cancelling.
    let private cancelResult handle (args: CancelArgs) : Task<obj> =
        let store = Commands.store handle

        match store.ReloadLocal() with
        | Error reason ->
            refused
                { Code = "PERSISTENCE_READ_FAILED"
                  Path = "inquiryId"
                  Message = reason }
        | Ok _ -> cancelFromCurrent handle args

    /// Registers the seven public tools. Each one decodes its own arguments and then
    /// goes to the one Runtime; none of them decides what comes next.
    let private registerTools (server: obj) (handle: RuntimeHandle) : unit =
        let register (tool: SphinxTool) (description: string) (inputSchema: obj) (handler: obj -> Task<obj>) =
            let config =
                createObj
                    [ "name" ==> Contract.toolName tool
                      "description" ==> description
                      "inputSchema" ==> zObjectOf zod inputSchema ]

            registerTool server (Contract.toolName tool) config (unaryHandler handler)
            |> ignore

        /// A tool whose arguments are decoded and then refused: the caller learns its
        /// arguments were read, and learns why the Runtime cannot yet carry the effect.
        let refusedAfterDecoding
            (tool: SphinxTool)
            (refusal: ToolRefusal)
            (decode: obj -> Result<'args, ToolRefusal>)
            (args: obj)
            : Task<obj> =
            match decode args with
            | Error decoded -> refused decoded
            | Ok _ -> refused refusal

        let startSchema =
            createObj
                [ "commandId"
                  ==> zString "Idempotent command identity; a repeat returns the original receipt"
                  "goalText" ==> zString "The user's goal text, stored byte-exact"
                  "constraints"
                  ==> zList (zString "one user constraint") "Supplementary constraints the user supplied"
                  "materialRefs"
                  ==> zList (zString "one material ref") "Content refs of material the user attached"
                  "authorizationRef" ==> zString "Reference proving the user supplied this goal"
                  "profileRef" ==> zString "The declared profile this inquiry runs under" ]

        let workNextSchema =
            createObj
                [ "commandId"
                  ==> zString "Idempotent command identity; a repeat returns the original receipt"
                  "inquiryId" ==> zString "The inquiry whose ready work is claimed"
                  "limit" ==> zCount "Maximum number of work items to claim" ]

        let workSubmitSchema =
            createObj
                [ "commandId"
                  ==> zString "Idempotent command identity; a repeat returns the original receipt"
                  "inquiryId" ==> zString "The inquiry that owns the work"
                  "workId" ==> zString "The work item this answer belongs to"
                  "attempt" ==> zCount "The attempt this answer belongs to"
                  "fence" ==> zString "The logical fence of that attempt"
                  "canonicalResult"
                  ==> zString "The worker's canonical answer bytes, kept exactly as returned"
                  "resultSchema"
                  ==> zSchemaRef (zString "Schema identity") (zString "Schema content hash")
                  "clusterId" ==> zString "Ballot cluster this answer belongs to" ]

        let statusSchema = createObj [ "inquiryId" ==> zString "The inquiry to read" ]

        let cancelSchema =
            createObj
                [ "commandId"
                  ==> zString "Idempotent command identity; a repeat returns the original receipt"
                  "inquiryId" ==> zString "The inquiry to stop"
                  "reason" ==> zString "Why the caller asked to stop" ]

        let exportSchema =
            createObj
                [ "inquiryId" ==> zString "The inquiry to export"
                  "mode"
                  ==> zString "summary redacts; full declares what the durable store makes replayable" ]

        let goalAmendSchema =
            createObj
                [ "commandId"
                  ==> zString "Idempotent command identity; a repeat returns the original receipt"
                  "inquiryId" ==> zString "The inquiry whose goal is amended"
                  "expectedRevision" ==> zString "The inquiry revision this amendment expects"
                  "authorizedBy" ==> zString "The user authorization reference for this amendment"
                  "addedConstraints"
                  ==> zList (zString "one added constraint") "Constraints the user added"
                  "replacementText"
                  ==> zOptional (zString "Replacement goal text, when the user reworded it") ]

        register
            SphinxTool.InquiryStart
            "Creates a durable v2 inquiry under explicitly configured authorization, preserving the original goal and returning its persisted command receipt. Planning and dispatch are not yet connected."
            startSchema
            (fun args ->
                task {
                    match Tool.decodeStart args with
                    | Error refusal -> return! refused refusal
                    | Ok decoded ->
                        let! result = Commands.start handle decoded
                        return! runtimeResult result
                })

        register
            SphinxTool.WorkNext
            "Claims up to limit ready work for the caller. Refused for now: the claim driver is not wired to durable work leasing. The call changes nothing and creates no lease."
            workNextSchema
            (refusedAfterDecoding
                SphinxTool.WorkNext
                (Tool.unsupported (Contract.toolName SphinxTool.WorkNext))
                Tool.decodeWorkNext)

        register
            SphinxTool.WorkSubmit
            "Submits the answer for the work the caller holds, bound to workId, attempt and fence. It carries the answer bytes and their schema and nothing else: a certificate patch, budget debit, event write or goal revision in the same call is refused by name. Refused for now: the submit driver is not wired to durable result admission and interpretation."
            workSubmitSchema
            (refusedAfterDecoding
                SphinxTool.WorkSubmit
                (Tool.unsupported (Contract.toolName SphinxTool.WorkSubmit))
                Tool.decodeWorkSubmit)

        register
            SphinxTool.InquiryStatus
            "Reads one inquiry: business status, revision, the Runtime's own next-step classification and the semantic view. It creates no lease, calls no model and changes nothing. An inquiry absent from the durable record is refused, never reported as an empty inquiry."
            statusSchema
            (fun args ->
                match Tool.decodeStatus args with
                | Error refusal -> refused refusal
                | Ok decoded -> Commands.status handle decoded |> runtimeResult)

        register
            SphinxTool.InquiryCancel
            "Requests cancellation of one inquiry through the same canonical fold the reader uses. An unconfirmed abort stays cancelling and never reports cancelled. Idempotent by commandId: a repeated commandId returns the original revision without appending again."
            cancelSchema
            (fun args ->
                match Tool.decodeCancel args with
                | Error refusal -> refused refusal
                | Ok decoded -> cancelResult handle decoded)

        register
            SphinxTool.InquiryExport
            "Exports one inquiry from its accepted canonical envelopes, with separate trace, state and semantic hashes and an explicit replayability declaration. It creates no lease, calls no model and changes no business state."
            exportSchema
            (fun args ->
                match Tool.decodeExport args with
                | Error refusal -> refused refusal
                | Ok decoded -> Commands.exportInquiry handle decoded |> runtimeResult)

        register
            SphinxTool.GoalAmend
            "Amends the goal with an explicit user authorizer. Refused for now: the amendment driver is not wired to durable user-authorized changes. The call changes nothing."
            goalAmendSchema
            (refusedAfterDecoding
                SphinxTool.GoalAmend
                (Tool.unsupported (Contract.toolName SphinxTool.GoalAmend))
                Tool.decodeGoalAmend)

    let private bindAppendCutUnknown (incident: AppendCutUnknownIncident) =
        let message =
            sprintf
                "inquiry %s; command %s; event %s: %s"
                (InquiryId.value incident.InquiryId)
                incident.CommandId
                (Wanxiangshu.Foundation.Identity.EventId.value incident.EventId)
                (AppendError.describe (AppendError.CommitUnknown incident.Evidence))

        FatalProcess.trip "sphinx-semantic-cut" message

    /// Boots the server against a durable store. The store already carries the v2 rule
    /// program, so replay happens through the same fold the caller reads.
    let serveConfigured (store: IEventStore) (configuration: obj option) : JS.Promise<unit> =
        match Commands.create store configuration bindAppendCutUnknown with
        | Error refusal ->
            consoleError (
                sprintf "[sphinx-mcp] configuration rejected: %s at %s: %s" refusal.Code refusal.Path refusal.Message
            )

            markStartupFailure ()
            resolved
        | Ok handle ->
            let server =
                construct
                    mcpServerConstructor
                    (createObj [ "name" ==> "sphinx"; "version" ==> Contract.apiVersion ])
                    (createObj [])

            registerTools server handle
            connect server (constructEmpty stdioTransportConstructor)

    let serve (store: IEventStore) : JS.Promise<unit> = serveConfigured store None

    let private parseConfiguration (text: string) : Result<obj, string> =
        try
            Ok(parseJson text)
        with error ->
            Error(sprintf "SPHINX_START_CONFIG must be valid JSON: %s" error.Message)

    let private readConfiguration () : Result<obj option, string> =
        let text = Environment.GetEnvironmentVariable "SPHINX_START_CONFIG"

        if isNull text then
            Ok None
        else
            parseConfiguration text |> Result.map Some

    let private bootConfigured (commonDir: string) (configuration: obj option) : JS.Promise<unit> =
        match Bind.createDurableStore commonDir (System.Guid.NewGuid().ToString("N")) with
        | Ok store -> serveConfigured store configuration
        | Error reason ->
            consoleError (sprintf "[sphinx-mcp] durable boot failed: %s" reason)
            markStartupFailure ()
            resolved

    /// Starts a stdio server, reporting a boot failure through stderr and exit code.
    let boot (commonDir: string) : JS.Promise<unit> =
        match readConfiguration () with
        | Error reason ->
            consoleError (sprintf "[sphinx-mcp] configuration boot failed: %s" reason)
            markStartupFailure ()
            resolved
        | Ok configuration -> bootConfigured commonDir configuration
