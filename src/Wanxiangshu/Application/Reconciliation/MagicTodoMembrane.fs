namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoAdmission
open Wanxiangshu.Domain.MagicTodoAfter
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Domain.MagicTodoSurface
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Durable half of the GrandRewrite Magic Todo membrane.
///
/// before: localize the persisted ToolPart, validate `{obligations:[{name,work}]}`
/// input, write canonical obligation bodies, append Prepared, then expose only
/// legacy sink rows to the builtin executor. after/recovery proves physical
/// success against that receipt before Accepted.
module MagicTodoMembrane =

    type PreparedBridge =
        { ManagerSessionId: SessionId
          ManagerLifeId: ManagerLifeId
          Prepared: TodoWritePrepared
          PreparedFactRef: EventId
          SettledOld: ObligationList
          NormalizedProposal: ObligationList
          CompatibilityRows: CompatibilityTodoRow list
          AlreadyAccepted: bool
          AcceptedOutputDigest: string option }

    type AcceptOutcome =
        { EnrichedResult: string
          NeedsDedicatedEnlist: bool
          NeedsEnsureReview: bool }

    /// DSL-class: Decision
    [<RequireQualifiedAccess>]
    type PrepareRejection =
        | NoOpenManagerLife
        | UnexpectedToolName of actual: string
        | SnapshotInputMismatch
        | Admission of MagicTodoReject
        | BlobRead of reason: string
        | BlobWrite of reason: string
        | BlobDigestMismatch of label: string
        | BlobDecode of reason: string
        | JournalAppend of reason: string
        | ProjectionInconsistent of reason: string

    [<RequireQualifiedAccess>]
    type AcceptRejection =
        | InputDigestMismatch
        | OutputDigestMismatch
        | Planner of AcceptReject
        | JournalAppend of reason: string

    let private managerLife (sessionId: SessionId) (projection: ProjectionSet) =
        AgentProjection.tryFind sessionId projection.AgentProjections
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

    let private readList
        (journal: AgentJournal)
        (label: string)
        (blobRef: BlobRef)
        (expectedDigest: BlobDigest)
        : Result<ObligationList, PrepareRejection> =
        match journal.Writer.BlobWriter.Read blobRef with
        | Error reason -> Error(PrepareRejection.BlobRead reason)
        | Ok body when HostDigest.sha256Hex body <> BlobDigest.value expectedDigest ->
            Error(PrepareRejection.BlobDigestMismatch label)
        | Ok body ->
            MagicTodoObligationCodec.tryDecode body
            |> Result.mapError PrepareRejection.BlobDecode

    let private writeList
        (journal: AgentJournal)
        (label: string)
        (items: ObligationList)
        : Result<BlobWriteReceipt, PrepareRejection> =
        let body = MagicTodoObligationCodec.encode items
        let expectedDigest = MagicTodo.obligationListDigest HostDigest.sha256Hex items

        match journal.WriteBlob body with
        | Error reason -> Error(PrepareRejection.BlobWrite reason)
        | Ok receipt when BlobDigest.value receipt.BlobDigest <> expectedDigest ->
            Error(PrepareRejection.BlobDigestMismatch label)
        | Ok receipt -> Ok receipt

    let private existingPrepared
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        (life: MagicTodoProjection.LifeMagicTodoState)
        : ExistingPrepared option =
        Map.tryFind (TodoWriteId.value writeId) life.Checkpoints
        |> Option.map (fun checkpoint ->
            { Identity =
                { ManagerLifeId = lifeId
                  ProviderInputDigest = checkpoint.ProviderInputDigest
                  BaseTodoDigest = BlobDigest.value checkpoint.BaseTodoDigest
                  ToolPartOrdinal = checkpoint.ToolPartOrdinal }
              TodoWriteId = checkpoint.TodoWriteId })

    let private preparedFromCheckpoint
        (lifeId: ManagerLifeId)
        (checkpoint: MagicTodoProjection.CheckpointRecord)
        : TodoWritePrepared =
        { ManagerSessionId = checkpoint.ManagerSessionId
          ManagerLifeId = lifeId
          TodoWriteId = checkpoint.TodoWriteId
          ToolCallId = checkpoint.ToolCallId
          ToolPartOrdinal = checkpoint.ToolPartOrdinal
          BaseTodoRef = checkpoint.BaseTodoRef
          BaseTodoDigest = checkpoint.BaseTodoDigest
          ProposedTodoRef = checkpoint.ProposedTodoRef
          ProposedTodoDigest = checkpoint.ProposedTodoDigest
          ProviderInputDigest = checkpoint.ProviderInputDigest
          ReviewFrontier = checkpoint.ReviewFrontier
          SemanticVersion = checkpoint.SemanticVersion }

    let private bridge
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (prepared: TodoWritePrepared)
        (preparedFactRef: EventId)
        (settledOld: ObligationList)
        (proposal: ObligationList)
        (alreadyAccepted: bool)
        (acceptedOutputDigest: string option)
        =
        { ManagerSessionId = managerSessionId
          ManagerLifeId = lifeId
          Prepared = prepared
          PreparedFactRef = preparedFactRef
          SettledOld = settledOld
          NormalizedProposal = proposal
          CompatibilityRows = obligationsToCompatibilityRows proposal
          AlreadyAccepted = alreadyAccepted
          AcceptedOutputDigest = acceptedOutputDigest }

    let prepare
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (locality: MagicTodoLocality.LocalizedToolCall)
        (providerInputDigest: string)
        (submitted: ObligationList)
        : Result<PreparedBridge, PrepareRejection> =
        let snapshotMatchesSubmitted =
            match MagicTodoObligationCodec.tryDecodeAccount locality.InputCanonical with
            | Ok snapshotAccount -> snapshotAccount = submitted
            | Error _ -> false

        if locality.ToolName <> "todowrite" then
            Error(PrepareRejection.UnexpectedToolName locality.ToolName)
        elif not snapshotMatchesSubmitted then
            Error PrepareRejection.SnapshotInputMismatch
        else
            let projection = AgentJournal.snapshot journal

            match managerLife managerSessionId projection with
            | None -> Error PrepareRejection.NoOpenManagerLife
            | Some managerLife ->
                let lifeId = managerLife.LifeId
                let todoProjection = projection.AgentProjections.MagicTodo

                let life =
                    Map.tryFind (ManagerLifeId.value lifeId) todoProjection.ByLife
                    |> Option.defaultValue (MagicTodoProjection.emptyLife lifeId)

                let settledResult =
                    match life.SettledCurrentRef with
                    | None -> Ok []
                    | Some(blobRef, digest) -> readList journal "SettledCurrent" blobRef digest

                match settledResult with
                | Error error -> Error error
                | Ok settledOld ->
                    let writeId = MagicTodo.todoWriteId HostDigest.sha256Hex lifeId locality.ToolCallId
                    let prior = existingPrepared lifeId writeId life

                    let admission =
                        MagicTodoAdmission.admitObligations
                            HostDigest.sha256Hex
                            lifeId
                            settledOld
                            (MagicTodoProjection.mayAdmitNewCheckpoint life)
                            prior
                            { ToolCallId = locality.ToolCallId
                              ToolPartOrdinal = locality.ToolPartOrdinal
                              TodowriteCallIdsInMessage = locality.TodowriteCallIdsInMessage
                              ReviewFrontier = locality.ReviewFrontier
                              ProviderInputDigest = providerInputDigest }
                            submitted

                    match admission with
                    | AdmissionOutcome.Rejected rejection -> Error(PrepareRejection.Admission rejection)
                    | AdmissionOutcome.IdempotentReplay replayWriteId ->
                        match Map.tryFind (TodoWriteId.value replayWriteId) life.Checkpoints with
                        | None -> Error(PrepareRejection.ProjectionInconsistent "replayed Prepared is absent")
                        | Some checkpoint ->
                            match
                                readList journal "ProposedTodo" checkpoint.ProposedTodoRef checkpoint.ProposedTodoDigest
                            with
                            | Error error -> Error error
                            | Ok proposal ->
                                Ok(
                                    bridge
                                        managerSessionId
                                        lifeId
                                        (preparedFromCheckpoint lifeId checkpoint)
                                        checkpoint.PreparedFactRef
                                        settledOld
                                        proposal
                                        checkpoint.Accepted
                                        checkpoint.OutputDigest
                                )
                    | AdmissionOutcome.FreshPrepare preparedPlan ->
                        match
                            writeList journal "BaseTodo" preparedPlan.Base,
                            writeList journal "ProposedTodo" preparedPlan.Proposed
                        with
                        | Error error, _
                        | _, Error error -> Error error
                        | Ok baseBlob, Ok proposedBlob ->
                            let prepared =
                                { ManagerSessionId = managerSessionId
                                  ManagerLifeId = lifeId
                                  TodoWriteId = preparedPlan.TodoWriteId
                                  ToolCallId = locality.ToolCallId
                                  ToolPartOrdinal = preparedPlan.ToolPartOrdinal
                                  BaseTodoRef = baseBlob.BlobRef
                                  BaseTodoDigest = baseBlob.BlobDigest
                                  ProposedTodoRef = proposedBlob.BlobRef
                                  ProposedTodoDigest = proposedBlob.BlobDigest
                                  ProviderInputDigest = preparedPlan.ProviderInputDigest
                                  ReviewFrontier = preparedPlan.ReviewFrontier
                                  SemanticVersion = MagicTodo.SemanticVersion }

                            match
                                AgentJournal.appendMagicTodo
                                    (StreamId.Session managerSessionId)
                                    (Some locality.ProviderRun)
                                    (MagicTodoFact.TodoWritePrepared prepared)
                                    journal
                            with
                            | Error failure ->
                                Error(PrepareRejection.JournalAppend(JournalAppendFailure.describe failure))
                            | Ok receipt ->
                                Ok(
                                    bridge
                                        managerSessionId
                                        lifeId
                                        prepared
                                        receipt.EventId
                                        settledOld
                                        preparedPlan.Proposed
                                        false
                                        None
                                )

    let accept
        (journal: AgentJournal)
        (bridge: PreparedBridge)
        (physical: PhysicalSuccessEvidence)
        (observedInputDigest: string)
        (observedOutputDigest: string)
        : Result<AcceptOutcome, AcceptRejection> =
        if bridge.Prepared.ProviderInputDigest <> observedInputDigest then
            Error AcceptRejection.InputDigestMismatch
        elif bridge.AlreadyAccepted then
            match bridge.AcceptedOutputDigest with
            | Some digest when digest = observedOutputDigest ->
                Ok
                    { EnrichedResult = ""
                      NeedsDedicatedEnlist = false
                      NeedsEnsureReview = false }
            | _ -> Error AcceptRejection.OutputDigestMismatch
        else
            let projection = AgentJournal.snapshot journal

            let life =
                Map.tryFind (ManagerLifeId.value bridge.ManagerLifeId) projection.AgentProjections.MagicTodo.ByLife
                |> Option.defaultValue (MagicTodoProjection.emptyLife bridge.ManagerLifeId)

            let checkpoint =
                Map.tryFind (TodoWriteId.value bridge.Prepared.TodoWriteId) life.Checkpoints

            let isT1Commitment = List.isEmpty life.AcceptedOrder

            let accepted =
                { ManagerLifeId = bridge.Prepared.ManagerLifeId
                  TodoWriteId = bridge.Prepared.TodoWriteId
                  ToolCallId = bridge.Prepared.ToolCallId
                  PreparedFactRef = bridge.PreparedFactRef
                  InputDigest = observedInputDigest
                  OutputDigest = observedOutputDigest
                  PhysicalSuccessEvidence = physical
                  SemanticVersion = bridge.Prepared.SemanticVersion }

            let lang = ProviderProse.languageOf bridge.ManagerSessionId

            let previousBody =
                ProviderProse.render lang MagicTodoSurface.Path.PreviousNone Map.empty

            let acceptedEpilogue =
                ProviderProse.render lang MagicTodoSurface.Path.ObligationAcceptedEpilogue Map.empty

            let rendered =
                ProviderProse.render
                    lang
                    MagicTodoSurface.Path.ObligationWriteResult
                    (MagicTodoSurface.obligationWriteSubs
                        previousBody
                        (MagicTodoSurface.renderObligationListWire bridge.NormalizedProposal)
                        (MagicTodoSurface.renderObligationListWire bridge.NormalizedProposal)
                        acceptedEpilogue)

            let enrichedResult =
                if isT1Commitment then
                    ManagerNarrative.wrapT1AcceptedResult
                        (ProviderProse.documentFor bridge.ManagerSessionId ManagerNarrative.Path.T1Revelation Map.empty)
                        rendered
                else
                    rendered

            match
                AgentJournal.appendMagicTodo
                    (StreamId.Session bridge.ManagerSessionId)
                    None
                    (MagicTodoFact.TodoWriteAccepted accepted)
                    journal
            with
            | Error failure -> Error(AcceptRejection.JournalAppend(JournalAppendFailure.describe failure))
            | Ok _ ->
                Ok
                    { EnrichedResult = enrichedResult
                      NeedsDedicatedEnlist = life.Dedicated.IsNone
                      NeedsEnsureReview = checkpoint |> Option.bind (fun value -> value.Concluded) |> Option.isNone }

/// Physical OpenCode V1 hook overlay for Magic Todo. The Host builtin remains
/// the executor/compatibility sink; this layer owns definition, durable prepare,
/// physical-success accept, and model-visible result enrichment.
module MagicTodoHostHooks =

    type HookSet =
        { Definition: obj -> obj -> unit
          Before: obj -> obj -> Task<unit>
          After: obj -> obj -> Task<unit> }

    let private requiredText (input: obj) (field: string) =
        if isNull input || isNull input?(field) then
            invalidOp (sprintf "Magic Todo hook requires %s" field)

        let value = string input?(field)

        if String.IsNullOrWhiteSpace value then
            invalidOp (sprintf "Magic Todo hook requires non-empty %s" field)

        value

    let private isTodoTool (input: obj) (field: string) =
        not (isNull input)
        && not (isNull input?(field))
        && string input?(field) = "todowrite"

    let private bridgeKey sessionId callId = sessionId + ":" + callId

    let private outputCanonical (output: obj) =
        if isNull output || isNull output?output then
            ""
        else
            CanonicalJson.canonicalJson output?output

    let create (journal: AgentJournal option) (snapshot: ISessionSnapshotPort option) : HookSet =
        let bridges = Dictionary<string, MagicTodoMembrane.PreparedBridge>()

        let definition (input: obj) (output: obj) =
            if isTodoTool input "toolID" then
                let sessionText =
                    if isNull input || isNull input?sessionID then
                        ""
                    else
                        string input?sessionID

                let lang =
                    if String.IsNullOrWhiteSpace sessionText then
                        ProviderLanguageBinding.readGlobalPreference ()
                    else
                        let sid = SessionId.create sessionText

                        match SessionProviderLanguage.tryGet sid with
                        | Some value -> value
                        | None -> ProviderLanguageBinding.readGlobalPreference ()

                MagicTodoHostCodec.applyDefinition lang output

        let before (input: obj) (output: obj) : Task<unit> =
            task {
                if isTodoTool input "tool" then
                    let durable =
                        match journal with
                        | Some value -> value
                        | None -> invalidOp "Magic Todo requires a durable AgentJournal"

                    let snapshots =
                        match snapshot with
                        | Some value -> value
                        | None -> invalidOp "Magic Todo requires the full session snapshot port"

                    let sessionText = requiredText input "sessionID"
                    let callText = requiredText input "callID"
                    let sessionId = SessionId.create sessionText
                    let callId = ToolCallId.create callText
                    let args: obj = output?args

                    match MagicTodoHostCodec.tryDecodeObligations args with
                    | Error reason -> invalidOp reason
                    | Ok obligations ->
                        let providerInputCanonical = MagicTodoHostCodec.canonicalInput args
                        let providerInputDigest = HostDigest.sha256Hex providerInputCanonical
                        let! messagesResult = snapshots.GetMessages sessionId

                        let messages =
                            match messagesResult with
                            | Ok value -> value
                            | Error reason -> invalidOp ("Magic Todo snapshot unavailable: " + reason)

                        let locality =
                            match
                                MagicTodoLocality.resolve sessionId messages (AgentJournal.snapshot durable) callId
                            with
                            | Ok value -> value
                            | Error reason -> invalidOp (sprintf "Magic Todo locality failed: %A" reason)

                        let prepared =
                            match
                                MagicTodoMembrane.prepare durable sessionId locality providerInputDigest obligations
                            with
                            | Ok value -> value
                            | Error reason -> invalidOp (sprintf "Magic Todo prepare failed: %A" reason)

                        bridges[bridgeKey sessionText callText] <- prepared
                        MagicTodoHostCodec.replaceCompatibilityArgs output prepared.CompatibilityRows
            }

        let after (input: obj) (output: obj) : Task<unit> =
            task {
                if isTodoTool input "tool" then
                    let durable =
                        match journal with
                        | Some value -> value
                        | None -> invalidOp "Magic Todo requires a durable AgentJournal"

                    let sessionText = requiredText input "sessionID"
                    let callText = requiredText input "callID"
                    let key = bridgeKey sessionText callText

                    match bridges.TryGetValue key with
                    | false, _ -> invalidOp "Magic Todo after hook has no prepared bridge"
                    | true, prepared ->
                        try
                            let outputDigest = outputCanonical output |> HostDigest.sha256Hex

                            match
                                MagicTodoMembrane.accept
                                    durable
                                    prepared
                                    PhysicalSuccessEvidence.LiveAfterSuccess
                                    prepared.Prepared.ProviderInputDigest
                                    outputDigest
                            with
                            | Error reason -> invalidOp (sprintf "Magic Todo accept failed: %A" reason)
                            | Ok accepted ->
                                if not (String.IsNullOrEmpty accepted.EnrichedResult) then
                                    MagicTodoHostCodec.replaceEnrichedResult output accepted.EnrichedResult
                        finally
                            bridges.Remove key |> ignore
            }

        { Definition = definition
          Before = before
          After = after }
