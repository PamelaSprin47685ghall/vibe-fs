namespace Wanxiangshu.Mission.Obligation.Todo

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoAdmission
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoSurface
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

/// Durable half of the GrandRewrite Magic Todo membrane.
///
/// before: localize the persisted ToolPart, validate
/// `{planComplete,workingOn,obligations:[{name,work}]}` input, write canonical
/// obligation bodies, append Prepared, then expose only
/// legacy sink rows to the builtin executor. after/recovery proves physical
/// success against that receipt before Accepted.
module MagicTodoMembrane =

    [<RequireQualifiedAccess>]
    type PreparedBridgeAcceptance =
        | AwaitingAcceptance
        | Accepted of outputDigest: string

    type PreparedBridge =
        { ManagerSessionId: SessionId
          ManagerLifeId: ManagerLifeId
          Prepared: TodoWritePrepared
          PreparedFactRef: EventId
          BaseObligations: ObligationList
          SubmittedObligations: ObligationList
          Acceptance: PreparedBridgeAcceptance }

    type AcceptOutcome = { EnrichedResult: string }

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
        taskResult {
            let! body =
                journal.Writer.BlobWriter.Read blobRef
                |> TaskResult.mapError PrepareRejection.BlobRead

            do!
                Result.requireTrue
                    (PrepareRejection.BlobDigestMismatch label)
                    (HostDigest.sha256Hex body = BlobDigest.value expectedDigest)

            return!
                MagicTodoObligationCodec.tryDecode body
                |> Result.mapError PrepareRejection.BlobDecode
        }

    let private writeList
        (journal: AgentJournal)
        (label: string)
        (items: ObligationList)
        : Task<Result<BlobWriteReceipt, PrepareRejection>> =
        taskResult {
            let body = MagicTodoObligationCodec.encode items
            let expectedDigest = MagicTodo.obligationListDigest HostDigest.sha256Hex items
            let! receipt = journal.WriteBlob body |> TaskResult.mapError PrepareRejection.BlobWrite

            do!
                Result.requireTrue
                    (PrepareRejection.BlobDigestMismatch label)
                    (BlobDigest.value receipt.BlobDigest = expectedDigest)

            return receipt
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
              TodoWriteId = checkpoint.TodoWriteId
              Acceptance =
                if MagicTodoProjection.isAccepted checkpoint then
                    MagicTodoAdmission.ExistingPreparedAcceptance.Accepted
                else
                    MagicTodoAdmission.ExistingPreparedAcceptance.PreparedOnly })

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
        (acceptance: PreparedBridgeAcceptance)
        =
        { ManagerSessionId = managerSessionId
          ManagerLifeId = lifeId
          Prepared = prepared
          PreparedFactRef = preparedFactRef
          BaseObligations = baseObligations
          SubmittedObligations = proposal
          Acceptance = acceptance }

    let private bridgeAcceptance (checkpoint: MagicTodoProjection.CheckpointRecord) : PreparedBridgeAcceptance =
        match MagicTodoProjection.acceptedEvidence checkpoint with
        | Some accepted -> PreparedBridgeAcceptance.Accepted accepted.OutputDigest
        | None -> PreparedBridgeAcceptance.AwaitingAcceptance

    let private snapshotMatchesSubmitted
        (locality: MagicTodoLocality.LocalizedToolCall)
        (planCompleteDeclared: bool)
        (submitted: ObligationList)
        =
        match MagicTodoObligationCodec.tryDecodeInput locality.InputCanonical with
        | Ok snapshotInput ->
            snapshotInput.PlanComplete = planCompleteDeclared
            && snapshotInput.Obligations = submitted
        | Error _ -> false

    let private readCurrentObligations (journal: AgentJournal) (life: MagicTodoProjection.LifeMagicTodoState) =
        match life.CurrentObligationsRef with
        | None -> Task.FromResult(Ok [])
        | Some(blobRef, digest) -> readList journal "CurrentObligations" blobRef digest

    let private replayPreparedBridge
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (life: MagicTodoProjection.LifeMagicTodoState)
        (currentObligations: ObligationList)
        (replayWriteId: TodoWriteId)
        : Task<Result<PreparedBridge, PrepareRejection>> =
        taskResult {
            let! checkpoint =
                Map.tryFind (TodoWriteId.value replayWriteId) life.Checkpoints
                |> Result.requireSome (PrepareRejection.ProjectionInconsistent "replayed Prepared is absent")

            let! proposal = readList journal "ProposedTodo" checkpoint.ProposedTodoRef checkpoint.ProposedTodoDigest

            return
                bridge
                    managerSessionId
                    lifeId
                    (preparedFromCheckpoint lifeId checkpoint)
                    checkpoint.PreparedFactRef
                    currentObligations
                    proposal
                    (bridgeAcceptance checkpoint)
        }

    let private freshPrepareBridge
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (locality: MagicTodoLocality.LocalizedToolCall)
        (planCompleteDeclared: bool)
        (currentObligations: ObligationList)
        (preparedPlan: MagicTodoAdmission.ObligationPrepareSuccess)
        : Task<Result<PreparedBridge, PrepareRejection>> =
        taskResult {
            let! baseBlob = writeList journal "BaseTodo" preparedPlan.Base
            let! proposedBlob = writeList journal "ProposedTodo" preparedPlan.Proposed

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

            let! receipt =
                AgentJournal.appendMagicTodo
                    (StreamId.Session managerSessionId)
                    (Some locality.ProviderRun)
                    (MagicTodoFact.TodoWritePrepared prepared)
                    journal
                |> TaskResult.mapError (fun failure ->
                    PrepareRejection.JournalAppend(JournalAppendFailure.describe failure))

            return
                bridge
                    managerSessionId
                    lifeId
                    prepared
                    receipt.EventId
                    currentObligations
                    preparedPlan.Proposed
                    PreparedBridgeAcceptance.AwaitingAcceptance
        }

    let private materializeAdmission
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (life: MagicTodoProjection.LifeMagicTodoState)
        (locality: MagicTodoLocality.LocalizedToolCall)
        (planCompleteDeclared: bool)
        (currentObligations: ObligationList)
        (admission: MagicTodoAdmission.AdmissionOutcome<MagicTodoAdmission.ObligationPrepareSuccess>)
        : Task<Result<PreparedBridge, PrepareRejection>> =
        match admission with
        | AdmissionOutcome.Rejected rejection -> Task.FromResult(Error(PrepareRejection.Admission rejection))
        | AdmissionOutcome.IdempotentReplay replayWriteId ->
            replayPreparedBridge journal managerSessionId lifeId life currentObligations replayWriteId
        | AdmissionOutcome.FreshPrepare preparedPlan ->
            freshPrepareBridge
                journal
                managerSessionId
                lifeId
                locality
                planCompleteDeclared
                currentObligations
                preparedPlan

    let private prepareForOpenLife
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (managerLife: LifeProjection)
        (locality: MagicTodoLocality.LocalizedToolCall)
        (providerInputDigest: string)
        (planCompleteDeclared: bool)
        (submitted: ObligationList)
        : Task<Result<PreparedBridge, PrepareRejection>> =
        taskResult {
            let lifeId = managerLife.LifeId
            let todoProjection = (AgentJournal.snapshot journal).AgentProjections.MagicTodo

            let life =
                Map.tryFind (ManagerLifeId.value lifeId) todoProjection.ByLife
                |> Option.defaultValue (MagicTodoProjection.emptyLife lifeId)

            let! currentObligations = readCurrentObligations journal life
            let writeId = MagicTodo.todoWriteId HostDigest.sha256Hex lifeId locality.ToolCallId
            let prior = existingPrepared lifeId writeId life

            let admission =
                MagicTodoAdmission.admitObligations
                    HostDigest.sha256Hex
                    lifeId
                    currentObligations
                    prior
                    { ToolCallId = locality.ToolCallId
                      ToolPartOrdinal = locality.ToolPartOrdinal
                      TodowriteCallIdsInMessage = locality.TodowriteCallIdsInMessage
                      ReviewFrontier = locality.ReviewFrontier
                      ProviderInputDigest = providerInputDigest }
                    submitted

            return!
                materializeAdmission
                    journal
                    managerSessionId
                    lifeId
                    life
                    locality
                    planCompleteDeclared
                    currentObligations
                    admission
        }

    let prepare
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (locality: MagicTodoLocality.LocalizedToolCall)
        (providerInputDigest: string)
        (planCompleteDeclared: bool)
        (submitted: ObligationList)
        : Task<Result<PreparedBridge, PrepareRejection>> =
        task {
            match
                locality.ToolName = "todowrite",
                snapshotMatchesSubmitted locality planCompleteDeclared submitted,
                managerLife managerSessionId (AgentJournal.snapshot journal)
            with
            | false, _, _ -> return Error(PrepareRejection.UnexpectedToolName locality.ToolName)
            | true, false, _ -> return Error PrepareRejection.SnapshotInputMismatch
            | true, true, None -> return Error PrepareRejection.NoOpenManagerLife
            | true, true, Some openLife ->
                return!
                    prepareForOpenLife
                        journal
                        managerSessionId
                        openLife
                        locality
                        providerInputDigest
                        planCompleteDeclared
                        submitted
        }

    let private acceptIdempotent
        (acceptedOutputDigest: string)
        (observedOutputDigest: string)
        : Result<AcceptOutcome, AcceptRejection> =
        if acceptedOutputDigest = observedOutputDigest then
            Ok { EnrichedResult = "" }
        else
            Error AcceptRejection.OutputDigestMismatch

    let private enrichAcceptedResult
        (managerSessionId: SessionId)
        (isT1Commitment: bool)
        (document: LlmFacing.Document)
        =
        let combined =
            if isT1Commitment then
                LlmFacing.combine
                    [ LlmFacing.instructions (
                          ProviderProse.instructionLines
                              (SessionProviderLanguage.languageOf managerSessionId)
                              ManagerNarrative.Path.T1Revelation
                              Map.empty
                      )
                      document ]
            else
                document

        LlmFacing.render combined

    let private acceptFresh
        (journal: AgentJournal)
        (bridge: PreparedBridge)
        (physical: PhysicalSuccessEvidence)
        (observedInputDigest: string)
        (observedOutputDigest: string)
        : Task<Result<AcceptOutcome, AcceptRejection>> =
        taskResult {
            let projection = AgentJournal.snapshot journal

            let life =
                Map.tryFind (ManagerLifeId.value bridge.ManagerLifeId) projection.AgentProjections.MagicTodo.ByLife
                |> Option.defaultValue (MagicTodoProjection.emptyLife bridge.ManagerLifeId)

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

            let lang = SessionProviderLanguage.languageOf bridge.ManagerSessionId

            let rendered =
                LlmFacing.instructions (
                    ProviderProse.instructionLines lang MagicTodoSurface.Path.ObligationAcceptedEpilogue Map.empty
                )

            let enrichedResult =
                enrichAcceptedResult bridge.ManagerSessionId isT1Commitment rendered

            let! _ =
                AgentJournal.appendMagicTodo
                    (StreamId.Session bridge.ManagerSessionId)
                    None
                    (MagicTodoFact.TodoWriteAccepted accepted)
                    journal
                |> TaskResult.mapError (fun failure ->
                    AcceptRejection.JournalAppend(JournalAppendFailure.describe failure))

            return { EnrichedResult = enrichedResult }
        }

    let accept
        (journal: AgentJournal)
        (bridge: PreparedBridge)
        (physical: PhysicalSuccessEvidence)
        (observedInputDigest: string)
        (observedOutputDigest: string)
        : Task<Result<AcceptOutcome, AcceptRejection>> =
        task {
            match bridge.Prepared.ProviderInputDigest = observedInputDigest, bridge.Acceptance with
            | false, _ -> return Error AcceptRejection.InputDigestMismatch
            | true, PreparedBridgeAcceptance.Accepted outputDigest ->
                return acceptIdempotent outputDigest observedOutputDigest
            | true, PreparedBridgeAcceptance.AwaitingAcceptance ->
                return! acceptFresh journal bridge physical observedInputDigest observedOutputDigest
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
            Wanxiangshu.Foundation.CanonicalJson.canonicalJson output?output

    let private sessionTextOf (input: obj) =
        if isNull input || isNull input?sessionID then
            ""
        else
            string input?sessionID

    let private languageForSession (sessionText: string) =
        ProviderLanguageBinding.forSessionText sessionText

    let private requirePort (reason: string) (port: 'a option) =
        match port with
        | Some value -> value
        | None -> fatalInfrastructure "" reason

    let private preparationFailure (sessionText: string) (reason: MagicTodoMembrane.PrepareRejection) =
        match syntaxPrepareFailure reason with
        | Some syntax -> ObligationLedgerWorkflow.PreparationAttempt.Failed syntax
        | None -> fatalInfrastructure sessionText (sprintf "prepare invariant failed: %A" reason)

    let private admitPreparationAttempt
        (durable: AgentJournal)
        (sessionId: SessionId)
        (sessionText: string)
        (locality: MagicTodoLocality.LocalizedToolCall)
        (providerInputDigest: string)
        (planComplete: bool)
        (obligations: ObligationList)
        =
        task {
            match!
                MagicTodoMembrane.prepare durable sessionId locality providerInputDigest planComplete obligations
            with
            | Ok value -> return ObligationLedgerWorkflow.PreparationAttempt.Prepared value
            | Error reason -> return preparationFailure sessionText reason
        }

    let private requireMessages (sessionText: string) (messagesResult: Result<SessionMessage list, string>) =
        match messagesResult with
        | Error reason -> fatalInfrastructure sessionText ("snapshot unavailable: " + reason)
        | Ok messages -> messages

    let private requireLocality
        (sessionText: string)
        (localityResult: Result<MagicTodoLocality.LocalizedToolCall, MagicTodoLocality.LocalityRejection>)
        =
        match localityResult with
        | Error reason -> fatalInfrastructure sessionText (sprintf "locality failed: %A" reason)
        | Ok locality -> locality

    let private requireMaterialized
        (sessionText: string)
        (materialized: Result<MagicTodoLocality.LocalizedToolCall, MagicTodoLocality.InputMaterializationRejection>)
        =
        match materialized with
        | Error reason -> fatalInfrastructure sessionText (sprintf "input materialization failed: %A" reason)
        | Ok locality -> locality

    let private resolvePreparationFailure sessionText failure : Result<MagicTodoMembrane.PreparedBridge, string> =
        match failure with
        | ObligationLedgerWorkflow.PreparationFailure.AttemptFailed syntax -> Error syntax

    let private prepareDeferredBridge
        (durable: AgentJournal)
        (snapshots: ISessionSnapshotPort)
        (sessionId: SessionId)
        (sessionText: string)
        (callId: ToolCallId)
        (providerInputCanonical: string)
        (planComplete: bool)
        (obligations: ObligationList)
        : Task<Result<MagicTodoMembrane.PreparedBridge, string>> =
        task {
            let! messagesResult = snapshots.GetMessages sessionId
            let messages = requireMessages sessionText messagesResult

            let currentProviderRun =
                match SessionSnapshot.locateToolCall callId messages with
                | Ok located -> located.ProviderRun
                | Error reason ->
                    fatalInfrastructure sessionText (sprintf "todowrite snapshot locality failed: %A" reason)

            let priorMessages =
                let currentRunId = ProviderRunIdentity.value currentProviderRun
                messages |> List.takeWhile (fun message -> message.Id <> currentRunId)

            // Synchronise only the complete transcript prefix before this provider
            // run. The current assistant message can still contain Host-created
            // pending tool stubs with `{}` input; capturing those now would make
            // transport construction state durable semantic XTrace. Locality below
            // accounts for current-message parts before this call without persisting
            // the unmaterialized call itself.
            match! XTraceCapture.captureSessionMessagesWithReceipt (Some durable) sessionId priorMessages with
            | Error error ->
                fatalInfrastructure sessionText (sprintf "XTrace transcript-prefix capture failed: %A" error)
            | Ok _ -> ()

            let locality =
                MagicTodoLocality.resolve sessionId messages (AgentJournal.snapshot durable) callId
                |> requireLocality sessionText
                |> fun initial -> MagicTodoLocality.materializeInput initial providerInputCanonical
                |> requireMaterialized sessionText

            let providerInputDigest = HostDigest.sha256Hex locality.InputCanonical

            let admitNow () =
                admitPreparationAttempt
                    durable
                    sessionId
                    sessionText
                    locality
                    providerInputDigest
                    planComplete
                    obligations

            let! outcome = ObligationLedgerWorkflow.prepareCheckpoint admitNow

            match outcome with
            | Ok value -> return Ok value
            | Error failure -> return resolvePreparationFailure sessionText failure
        }

    let private unwrapPrepared
        (preparedResult: Result<MagicTodoMembrane.PreparedBridge, string>)
        : MagicTodoMembrane.PreparedBridge =
        match preparedResult with
        | Ok value -> value
        | Error syntaxReason -> invalidOp syntaxReason

    let private applyEnrichedResult (output: obj) (accepted: MagicTodoMembrane.AcceptOutcome) =
        if not (String.IsNullOrEmpty accepted.EnrichedResult) then
            MagicTodoHostCodec.replaceEnrichedResult output accepted.EnrichedResult

    let private acceptResolvedCheckpoint sessionText outcome (output: obj) =
        match outcome with
        | Error(ObligationLedgerWorkflow.AcceptanceFailure.AcceptFailed reason) ->
            fatalInfrastructure sessionText (sprintf "Magic Todo accept invariant failed: %A" reason)
        | Ok accepted -> applyEnrichedResult output accepted

    let private acceptAfterPrepare
        (durable: AgentJournal)
        (sessionText: string)
        (preparedTask: Task<Result<MagicTodoMembrane.PreparedBridge, string>>)
        (output: obj)
        =
        task {
            let! preparedResult = preparedTask
            let prepared = unwrapPrepared preparedResult
            let outputDigest = outputCanonical output |> HostDigest.sha256Hex

            let acceptDurably () =
                MagicTodoMembrane.accept
                    durable
                    prepared
                    PhysicalSuccessEvidence.LiveAfterSuccess
                    prepared.Prepared.ProviderInputDigest
                    outputDigest

            let! outcome = ObligationLedgerWorkflow.acceptCheckpoint acceptDurably

            acceptResolvedCheckpoint sessionText outcome output
        }

    let private runAfterTodo
        (journal: AgentJournal option)
        (bridges: Dictionary<string, Task<Result<MagicTodoMembrane.PreparedBridge, string>>>)
        (input: obj)
        (output: obj)
        =
        task {
            let durable = requirePort "Magic Todo requires a durable AgentJournal" journal
            let sessionText = requiredText input "sessionID"
            let callText = requiredText input "callID"
            let key = bridgeKey sessionText callText

            let preparedTask =
                match bridges.TryGetValue key with
                | false, _ -> fatalInfrastructure sessionText "Magic Todo after hook has no deferred prepare"
                | true, value -> value

            try
                do! acceptAfterPrepare durable sessionText preparedTask output
            finally
                bridges.Remove key |> ignore
        }

    let private runBeforeTodo
        (journal: AgentJournal option)
        (snapshot: ISessionSnapshotPort option)
        (bridges: Dictionary<string, Task<Result<MagicTodoMembrane.PreparedBridge, string>>>)
        (input: obj)
        (output: obj)
        =
        task {
            let durable = requirePort "Magic Todo requires a durable AgentJournal" journal

            let snapshots =
                requirePort "Magic Todo requires the full session snapshot port" snapshot

            let sessionText = requiredText input "sessionID"
            let callText = requiredText input "callID"
            let sessionId = SessionId.create sessionText
            let callId = ToolCallId.create callText
            let args: obj = output?args
            let submittedInput = MagicTodoHostCodec.decodeInputOrReject args
            let obligations = submittedInput.Obligations
            let providerInputCanonical = MagicTodoHostCodec.canonicalInput args

            MagicTodoHostCodec.replaceCompatibilityArgs
                output
                (obligationsToCompatibilityRows submittedInput.WorkingOn obligations)

            bridges[bridgeKey sessionText callText] <-
                prepareDeferredBridge
                    durable
                    snapshots
                    sessionId
                    sessionText
                    callId
                    providerInputCanonical
                    submittedInput.PlanComplete
                    obligations
        }

    let create (journal: AgentJournal option) (snapshot: ISessionSnapshotPort option) : HookSet =
        let bridges =
            Dictionary<string, Task<Result<MagicTodoMembrane.PreparedBridge, string>>>()

        let definition (input: obj) (output: obj) =
            if isTodoTool input "toolID" then
                let sessionText = sessionTextOf input
                let lang = languageForSession sessionText
                MagicTodoHostCodec.applyDefinition lang output

        let before (input: obj) (output: obj) : Task<unit> =
            task {
                if isTodoTool input "tool" then
                    do! runBeforeTodo journal snapshot bridges input output
            }

        let after (input: obj) (output: obj) : Task<unit> =
            task {
                if isTodoTool input "tool" then
                    do! runAfterTodo journal bridges input output
            }

        { Definition = definition
          Before = before
          After = after }
