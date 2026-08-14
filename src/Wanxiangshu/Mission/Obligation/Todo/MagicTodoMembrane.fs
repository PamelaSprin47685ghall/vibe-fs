namespace Wanxiangshu.Mission.Obligation.Todo

open System
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Domain
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoAdmission
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Domain.MagicTodoSurface
open Wanxiangshu.Host
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Manager
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Resources
open Wanxiangshu.Review
open Wanxiangshu.Session

/// Durable half of the GrandRewrite Magic Todo membrane.
///
/// before: localize the persisted ToolPart, validate `{planComplete,obligations:[{name,work}]}`
/// input, write canonical obligation bodies, append Prepared, then expose only
/// legacy sink rows to the builtin executor. after/recovery proves physical
/// success against that receipt before Accepted.
module MagicTodoMembrane =

    type PreparedBridge =
        { ManagerSessionId: SessionId
          ManagerLifeId: ManagerLifeId
          Prepared: TodoWritePrepared
          PreparedFactRef: EventId
          BaseObligations: ObligationList
          SubmittedObligations: ObligationList
          PreviousReview: PreviousReviewView option
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
        /// TODO-006 lag-1 wait: T(k+1) blocks until ConsumableReview is durable.
        /// Host deferred prepare awaits this; it is not invalidOp red text.
        | AwaitingConsumableReview of pendingTodoWriteId: string
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
        : Task<Result<ObligationList, PrepareRejection>> =
        task {
            match! journal.Writer.BlobWriter.Read blobRef with
            | Error reason -> return Error(PrepareRejection.BlobRead reason)
            | Ok body when HostDigest.sha256Hex body <> BlobDigest.value expectedDigest ->
                return Error(PrepareRejection.BlobDigestMismatch label)
            | Ok body ->
                return
                    MagicTodoObligationCodec.tryDecode body
                    |> Result.mapError PrepareRejection.BlobDecode
        }

    let private readText
        (journal: AgentJournal)
        (label: string)
        (blobRef: BlobRef)
        (expectedDigest: BlobDigest)
        : Task<Result<string, PrepareRejection>> =
        task {
            match! journal.Writer.BlobWriter.Read blobRef with
            | Error reason -> return Error(PrepareRejection.BlobRead reason)
            | Ok body when HostDigest.sha256Hex body <> BlobDigest.value expectedDigest ->
                return Error(PrepareRejection.BlobDigestMismatch label)
            | Ok body -> return Ok body
        }

    let private writeList
        (journal: AgentJournal)
        (label: string)
        (items: ObligationList)
        : Task<Result<BlobWriteReceipt, PrepareRejection>> =
        task {
            let body = MagicTodoObligationCodec.encode items
            let expectedDigest = MagicTodo.obligationListDigest HostDigest.sha256Hex items

            match! journal.WriteBlob body with
            | Error reason -> return Error(PrepareRejection.BlobWrite reason)
            | Ok receipt when BlobDigest.value receipt.BlobDigest <> expectedDigest ->
                return Error(PrepareRejection.BlobDigestMismatch label)
            | Ok receipt -> return Ok receipt
        }

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
          PlanCompleteDeclared = checkpoint.PlanCompleteDeclared
          ProviderInputDigest = checkpoint.ProviderInputDigest
          ReviewFrontier = checkpoint.ReviewFrontier
          SemanticVersion = checkpoint.SemanticVersion }

    let private bridge
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (prepared: TodoWritePrepared)
        (preparedFactRef: EventId)
        (baseObligations: ObligationList)
        (proposal: ObligationList)
        (previousReview: PreviousReviewView option)
        (alreadyAccepted: bool)
        (acceptedOutputDigest: string option)
        =
        { ManagerSessionId = managerSessionId
          ManagerLifeId = lifeId
          Prepared = prepared
          PreparedFactRef = preparedFactRef
          BaseObligations = baseObligations
          SubmittedObligations = proposal
          PreviousReview = previousReview
          CompatibilityRows = obligationsToCompatibilityRows proposal
          AlreadyAccepted = alreadyAccepted
          AcceptedOutputDigest = acceptedOutputDigest }

    let prepare
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (locality: MagicTodoLocality.LocalizedToolCall)
        (providerInputDigest: string)
        (planCompleteDeclared: bool)
        (submitted: ObligationList)
        : Task<Result<PreparedBridge, PrepareRejection>> =
        task {
            let snapshotMatchesSubmitted =
                match MagicTodoObligationCodec.tryDecodeInput locality.InputCanonical with
                | Ok snapshotInput ->
                    snapshotInput.PlanComplete = planCompleteDeclared
                    && snapshotInput.Obligations = submitted
                | Error _ -> false

            if locality.ToolName <> "todowrite" then
                return Error(PrepareRejection.UnexpectedToolName locality.ToolName)
            elif not snapshotMatchesSubmitted then
                return Error PrepareRejection.SnapshotInputMismatch
            else
                let projection = AgentJournal.snapshot journal

                match managerLife managerSessionId projection with
                | None -> return Error PrepareRejection.NoOpenManagerLife
                | Some managerLife ->
                    let lifeId = managerLife.LifeId
                    let todoProjection = projection.AgentProjections.MagicTodo

                    let life =
                        Map.tryFind (ManagerLifeId.value lifeId) todoProjection.ByLife
                        |> Option.defaultValue (MagicTodoProjection.emptyLife lifeId)

                    let! currentResult =
                        match life.CurrentObligationsRef with
                        | None -> Task.FromResult(Ok [])
                        | Some(blobRef, digest) -> readList journal "CurrentObligations" blobRef digest

                    let! previousReviewResult =
                        match MagicTodoProjection.consumablePreviousReview life with
                        | None -> Task.FromResult(Ok None)
                        | Some concluded ->
                            task {
                                match!
                                    readText
                                        journal
                                        "ProcessReviewLWR"
                                        concluded.WorkRecordRef
                                        concluded.WorkRecordDigest
                                with
                                | Error error -> return Error error
                                | Ok report ->
                                    return
                                        Ok(
                                            Some
                                                { Verdict = concluded.Verdict
                                                  ReportText = report }
                                        )
                            }

                    match currentResult, previousReviewResult with
                    | Error error, _
                    | _, Error error -> return Error error
                    | Ok currentObligations, Ok previousReview ->
                        let writeId = MagicTodo.todoWriteId HostDigest.sha256Hex lifeId locality.ToolCallId
                        let prior = existingPrepared lifeId writeId life

                        let admission =
                            MagicTodoAdmission.admitObligations
                                HostDigest.sha256Hex
                                lifeId
                                currentObligations
                                (MagicTodoProjection.mayAdmitNewCheckpoint life)
                                prior
                                { ToolCallId = locality.ToolCallId
                                  ToolPartOrdinal = locality.ToolPartOrdinal
                                  TodowriteCallIdsInMessage = locality.TodowriteCallIdsInMessage
                                  ReviewFrontier = locality.ReviewFrontier
                                  ProviderInputDigest = providerInputDigest }
                                submitted

                        match admission with
                        | AdmissionOutcome.AwaitingConsumableReview pending ->
                            return Error(PrepareRejection.AwaitingConsumableReview pending)
                        | AdmissionOutcome.Rejected rejection -> return Error(PrepareRejection.Admission rejection)
                        | AdmissionOutcome.IdempotentReplay replayWriteId ->
                            match Map.tryFind (TodoWriteId.value replayWriteId) life.Checkpoints with
                            | None ->
                                return Error(PrepareRejection.ProjectionInconsistent "replayed Prepared is absent")
                            | Some checkpoint ->
                                match!
                                    readList
                                        journal
                                        "ProposedTodo"
                                        checkpoint.ProposedTodoRef
                                        checkpoint.ProposedTodoDigest
                                with
                                | Error error -> return Error error
                                | Ok proposal ->
                                    return
                                        Ok(
                                            bridge
                                                managerSessionId
                                                lifeId
                                                (preparedFromCheckpoint lifeId checkpoint)
                                                checkpoint.PreparedFactRef
                                                currentObligations
                                                proposal
                                                previousReview
                                                checkpoint.Accepted
                                                checkpoint.OutputDigest
                                        )
                        | AdmissionOutcome.FreshPrepare preparedPlan ->
                            let! baseResult = writeList journal "BaseTodo" preparedPlan.Base
                            let! proposedResult = writeList journal "ProposedTodo" preparedPlan.Proposed

                            match baseResult, proposedResult with
                            | Error error, _
                            | _, Error error -> return Error error
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
                                      PlanCompleteDeclared = planCompleteDeclared
                                      ProviderInputDigest = preparedPlan.ProviderInputDigest
                                      ReviewFrontier = preparedPlan.ReviewFrontier
                                      SemanticVersion = MagicTodo.SemanticVersion }

                                match!
                                    AgentJournal.appendMagicTodo
                                        (StreamId.Session managerSessionId)
                                        (Some locality.ProviderRun)
                                        (MagicTodoFact.TodoWritePrepared prepared)
                                        journal
                                with
                                | Error failure ->
                                    return Error(PrepareRejection.JournalAppend(JournalAppendFailure.describe failure))
                                | Ok receipt ->
                                    return
                                        Ok(
                                            bridge
                                                managerSessionId
                                                lifeId
                                                prepared
                                                receipt.EventId
                                                currentObligations
                                                preparedPlan.Proposed
                                                previousReview
                                                false
                                                None
                                        )
        }

    let accept
        (journal: AgentJournal)
        (bridge: PreparedBridge)
        (physical: PhysicalSuccessEvidence)
        (observedInputDigest: string)
        (observedOutputDigest: string)
        : Task<Result<AcceptOutcome, AcceptRejection>> =
        task {
            if bridge.Prepared.ProviderInputDigest <> observedInputDigest then
                return Error AcceptRejection.InputDigestMismatch
            elif bridge.AlreadyAccepted then
                match bridge.AcceptedOutputDigest with
                | Some digest when digest = observedOutputDigest ->
                    return
                        Ok
                            { EnrichedResult = ""
                              NeedsDedicatedEnlist = false
                              NeedsEnsureReview = false }
                | _ -> return Error AcceptRejection.OutputDigestMismatch
            else
                let projection = AgentJournal.snapshot journal

                let life =
                    Map.tryFind (ManagerLifeId.value bridge.ManagerLifeId) projection.AgentProjections.MagicTodo.ByLife
                    |> Option.defaultValue (MagicTodoProjection.emptyLife bridge.ManagerLifeId)

                let checkpoint =
                    Map.tryFind (TodoWriteId.value bridge.Prepared.TodoWriteId) life.Checkpoints

                let isT1Commitment =
                    life.FirstPlanCommitment.IsNone && bridge.Prepared.PlanCompleteDeclared

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
                    match bridge.PreviousReview with
                    | Some previous when previous.Verdict = ProcessReviewVerdict.Revise ->
                        ProviderProse.render
                            lang
                            MagicTodoSurface.Path.PreviousReviewBody
                            (MagicTodoSurface.previousReviewSubs
                                (ProcessReviewVerdict.wire previous.Verdict)
                                previous.ReportText)
                    | _ -> ""

                let acceptedEpilogue =
                    ProviderProse.render lang MagicTodoSurface.Path.ObligationAcceptedEpilogue Map.empty

                let rendered =
                    ProviderProse.render
                        lang
                        MagicTodoSurface.Path.ObligationWriteResult
                        (MagicTodoSurface.obligationWriteSubs previousBody acceptedEpilogue)

                let enrichedResult =
                    if isT1Commitment then
                        ManagerNarrative.wrapT1AcceptedResult
                            (ProviderProse.documentFor
                                bridge.ManagerSessionId
                                ManagerNarrative.Path.T1Revelation
                                Map.empty)
                            rendered
                    else
                        rendered

                match!
                    AgentJournal.appendMagicTodo
                        (StreamId.Session bridge.ManagerSessionId)
                        None
                        (MagicTodoFact.TodoWriteAccepted accepted)
                        journal
                with
                | Error failure -> return Error(AcceptRejection.JournalAppend(JournalAppendFailure.describe failure))
                | Ok _ ->
                    return
                        Ok
                            { EnrichedResult = enrichedResult
                              NeedsDedicatedEnlist = life.Dedicated.IsNone
                              NeedsEnsureReview =
                                checkpoint |> Option.bind (fun value -> value.Concluded) |> Option.isNone }
        }

/// Physical OpenCode V1 hook overlay for Magic Todo. The Host builtin remains
/// the executor/compatibility sink; this layer owns definition, durable prepare,
/// physical-success accept, and model-visible result enrichment.
module MagicTodoHostHooks =

    type HookSet =
        { Definition: obj -> obj -> unit
          Before: obj -> obj -> Task<unit>
          After: obj -> obj -> Task<unit> }

    let private fatalInfrastructure (sessionText: string) (reason: string) : 'T =
        let fields =
            if String.IsNullOrWhiteSpace sessionText then
                [ "result", reason ]
            else
                [ "session_id", sessionText; "result", reason ]

        Diagnostic.fatal "magic-todo-infrastructure-failed" fields
        failwith ("unreachable after Diagnostic.fatal: " + reason)

    let private requiredText (input: obj) (field: string) =
        if isNull input || isNull input?(field) then
            fatalInfrastructure "" (sprintf "Magic Todo hook requires %s" field)

        let value = string input?(field)

        if String.IsNullOrWhiteSpace value then
            fatalInfrastructure "" (sprintf "Magic Todo hook requires non-empty %s" field)

        value

    let private syntaxPrepareFailure (reason: MagicTodoMembrane.PrepareRejection) : string option =
        match reason with
        | MagicTodoMembrane.PrepareRejection.Admission(MagicTodoReject.MultipleTodowriteInMessage callIds) ->
            Some(sprintf "todowrite may appear only once in an assistant message; calls=%A" callIds)
        | MagicTodoMembrane.PrepareRejection.Admission(MagicTodoReject.EmptyObligationName ordinal) ->
            Some(sprintf "todowrite obligation.name must be non-empty at index %d" ordinal)
        | MagicTodoMembrane.PrepareRejection.Admission(MagicTodoReject.DuplicateObligationName name) ->
            Some(sprintf "todowrite duplicate obligation name '%s'" name)
        | _ -> None

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

    let create
        (journal: AgentJournal option)
        (snapshot: ISessionSnapshotPort option)
        (processReview: ProcessReviewPort option)
        : HookSet =
        let bridges =
            Dictionary<string, Task<Result<MagicTodoMembrane.PreparedBridge, string>>>()

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
                        | None -> fatalInfrastructure "" "Magic Todo requires a durable AgentJournal"

                    let snapshots =
                        match snapshot with
                        | Some value -> value
                        | None -> fatalInfrastructure "" "Magic Todo requires the full session snapshot port"

                    let reviews =
                        match processReview with
                        | Some value -> value
                        | None -> fatalInfrastructure "" "Magic Todo requires the process review runtime"

                    let sessionText = requiredText input "sessionID"
                    let callText = requiredText input "callID"
                    let sessionId = SessionId.create sessionText
                    let callId = ToolCallId.create callText
                    let args: obj = output?args

                    match MagicTodoHostCodec.tryDecodeInput args with
                    | Error reason -> invalidOp reason
                    | Ok submittedInput ->
                        let obligations = submittedInput.Obligations
                        let providerInputCanonical = MagicTodoHostCodec.canonicalInput args
                        MagicTodoHostCodec.replaceCompatibilityArgs output (obligationsToCompatibilityRows obligations)

                        let prepared =
                            task {
                                let! messagesResult = snapshots.GetMessages sessionId

                                match messagesResult with
                                | Error reason ->
                                    return fatalInfrastructure sessionText ("snapshot unavailable: " + reason)
                                | Ok messages ->
                                    match
                                        MagicTodoLocality.resolve
                                            sessionId
                                            messages
                                            (AgentJournal.snapshot durable)
                                            callId
                                    with
                                    | Error reason ->
                                        return fatalInfrastructure sessionText (sprintf "locality failed: %A" reason)
                                    | Ok initialLocality ->
                                        match
                                            MagicTodoLocality.materializeInput initialLocality providerInputCanonical
                                        with
                                        | Error reason ->
                                            return
                                                fatalInfrastructure
                                                    sessionText
                                                    (sprintf "input materialization failed: %A" reason)
                                        | Ok locality ->
                                            let providerInputDigest = HostDigest.sha256Hex locality.InputCanonical

                                            let admitNow () =
                                                task {
                                                    match!
                                                        MagicTodoMembrane.prepare
                                                            durable
                                                            sessionId
                                                            locality
                                                            providerInputDigest
                                                            submittedInput.PlanComplete
                                                            obligations
                                                    with
                                                    | Ok value ->
                                                        return ObligationLedgerWorkflow.PreparationAttempt.Prepared value
                                                    | Error(MagicTodoMembrane.PrepareRejection.AwaitingConsumableReview _) ->
                                                        return ObligationLedgerWorkflow.PreparationAttempt.AwaitPreviousReview
                                                    | Error reason ->
                                                        match syntaxPrepareFailure reason with
                                                        | Some syntax ->
                                                            return ObligationLedgerWorkflow.PreparationAttempt.Failed syntax
                                                        | None ->
                                                            return
                                                                fatalInfrastructure
                                                                    sessionText
                                                                    (sprintf "prepare invariant failed: %A" reason)
                                                }

                                            let currentPendingReview () =
                                                let snapshotNow = AgentJournal.snapshot durable

                                                AgentProjection.tryFind sessionId snapshotNow.AgentProjections
                                                |> Option.bind (fun session -> session.ManagerLife)
                                                |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                                                |> Option.bind (fun mlife ->
                                                    MagicTodoProjection.tryLife
                                                        mlife.LifeId
                                                        snapshotNow.AgentProjections.MagicTodo
                                                    |> Option.bind MagicTodoProjection.pendingReviewObligation
                                                    |> Option.map (fun cp -> mlife.LifeId, cp.TodoWriteId))

                                            let awaitReview (lifeId, writeId) =
                                                reviews.AwaitConsumableReview durable sessionId lifeId writeId

                                            match!
                                                ObligationLedgerWorkflow.prepareCheckpoint
                                                    admitNow
                                                    currentPendingReview
                                                    awaitReview
                                            with
                                            | Ok value -> return Ok value
                                            | Error(ObligationLedgerWorkflow.PreparationFailure.AttemptFailed syntax) ->
                                                return Error syntax
                                            | Error(ObligationLedgerWorkflow.PreparationFailure.ReviewWaitFailed reason) ->
                                                return
                                                    fatalInfrastructure
                                                        sessionText
                                                        ("await ConsumableReview failed: " + reason)
                                            | Error ObligationLedgerWorkflow.PreparationFailure.MissingPendingReview ->
                                                return
                                                    fatalInfrastructure
                                                        sessionText
                                                        "lag-1 wait without pending ConsumableReview (projection inconsistent)"
                                            | Error ObligationLedgerWorkflow.PreparationFailure.ReviewDidNotConverge ->
                                                return
                                                    fatalInfrastructure
                                                        sessionText
                                                        "ConsumableReview wait completed but checkpoint admission still reports the same pending review"
                            }

                        bridges[bridgeKey sessionText callText] <- prepared
            }

        let after (input: obj) (output: obj) : Task<unit> =
            task {
                if isTodoTool input "tool" then
                    let durable =
                        match journal with
                        | Some value -> value
                        | None -> fatalInfrastructure "" "Magic Todo requires a durable AgentJournal"

                    let sessionText = requiredText input "sessionID"
                    let callText = requiredText input "callID"
                    let key = bridgeKey sessionText callText

                    match bridges.TryGetValue key with
                    | false, _ -> fatalInfrastructure sessionText "Magic Todo after hook has no deferred prepare"
                    | true, preparedTask ->
                        try
                            let! preparedResult = preparedTask

                            let prepared =
                                match preparedResult with
                                | Ok value -> value
                                | Error syntaxReason -> invalidOp syntaxReason

                            let outputDigest = outputCanonical output |> HostDigest.sha256Hex

                            let acceptDurably () =
                                MagicTodoMembrane.accept
                                    durable
                                    prepared
                                    PhysicalSuccessEvidence.LiveAfterSuccess
                                    prepared.Prepared.ProviderInputDigest
                                    outputDigest

                            let shouldEnsureReview (accepted: MagicTodoMembrane.AcceptOutcome) =
                                accepted.NeedsEnsureReview || accepted.NeedsDedicatedEnlist

                            let ensureReview (_accepted: MagicTodoMembrane.AcceptOutcome) =
                                match processReview with
                                | None ->
                                    Task.FromResult(Error "Magic Todo process review runtime disappeared after before")
                                | Some port ->
                                    port.EnsureReview
                                        durable
                                        prepared.ManagerSessionId
                                        prepared.ManagerLifeId
                                        prepared.Prepared.TodoWriteId

                            match!
                                ObligationLedgerWorkflow.acceptCheckpoint
                                    acceptDurably
                                    shouldEnsureReview
                                    ensureReview
                            with
                            | Error(ObligationLedgerWorkflow.AcceptanceFailure.AcceptFailed reason) ->
                                fatalInfrastructure
                                    sessionText
                                    (sprintf "Magic Todo accept invariant failed: %A" reason)
                            | Error(ObligationLedgerWorkflow.AcceptanceFailure.ReviewFailed reason) ->
                                fatalInfrastructure
                                    sessionText
                                    ("Magic Todo ensureReview infrastructure failed: " + reason)
                            | Ok accepted ->
                                if not (String.IsNullOrEmpty accepted.EnrichedResult) then
                                    MagicTodoHostCodec.replaceEnrichedResult output accepted.EnrichedResult
                        finally
                            bridges.Remove key |> ignore
            }

        { Definition = definition
          Before = before
          After = after }
