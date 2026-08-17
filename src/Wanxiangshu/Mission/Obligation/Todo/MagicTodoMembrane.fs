namespace Wanxiangshu.Mission.Obligation.Todo

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoAdmission
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoSurface
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Composition.Durable
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Review

/// Durable half of the GrandRewrite Magic Todo membrane.
///
/// before: localize the persisted ToolPart, validate
/// `{planComplete,workingOn,obligations:[{name,work}]}` input, write canonical
/// obligation bodies, append Prepared, then expose only
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

    let private readText
        (journal: AgentJournal)
        (label: string)
        (blobRef: BlobRef)
        (expectedDigest: BlobDigest)
        : Task<Result<string, PrepareRejection>> =
        taskResult {
            let! body =
                journal.Writer.BlobWriter.Read blobRef
                |> TaskResult.mapError PrepareRejection.BlobRead

            do!
                Result.requireTrue
                    (PrepareRejection.BlobDigestMismatch label)
                    (HostDigest.sha256Hex body = BlobDigest.value expectedDigest)

            return body
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
          AlreadyAccepted = alreadyAccepted
          AcceptedOutputDigest = acceptedOutputDigest }

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

    let private readPreviousReviewView (journal: AgentJournal) (life: MagicTodoProjection.LifeMagicTodoState) =
        match MagicTodoProjection.consumablePreviousReview life with
        | None -> Task.FromResult(Ok None)
        | Some concluded ->
            taskResult {
                let! report = readText journal "ProcessReviewLWR" concluded.WorkRecordRef concluded.WorkRecordDigest

                return
                    Some
                        { Verdict = concluded.Verdict
                          ReportText = report }
            }

    let private replayPreparedBridge
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (life: MagicTodoProjection.LifeMagicTodoState)
        (currentObligations: ObligationList)
        (previousReview: PreviousReviewView option)
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
                    previousReview
                    checkpoint.Accepted
                    checkpoint.OutputDigest
        }

    let private freshPrepareBridge
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (locality: MagicTodoLocality.LocalizedToolCall)
        (planCompleteDeclared: bool)
        (currentObligations: ObligationList)
        (previousReview: PreviousReviewView option)
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
                    previousReview
                    false
                    None
        }

    let private materializeAdmission
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (life: MagicTodoProjection.LifeMagicTodoState)
        (locality: MagicTodoLocality.LocalizedToolCall)
        (planCompleteDeclared: bool)
        (currentObligations: ObligationList)
        (previousReview: PreviousReviewView option)
        (admission: MagicTodoAdmission.AdmissionOutcome<MagicTodoAdmission.ObligationPrepareSuccess>)
        : Task<Result<PreparedBridge, PrepareRejection>> =
        match admission with
        | AdmissionOutcome.AwaitingConsumableReview pending ->
            Task.FromResult(Error(PrepareRejection.AwaitingConsumableReview pending))
        | AdmissionOutcome.Rejected rejection -> Task.FromResult(Error(PrepareRejection.Admission rejection))
        | AdmissionOutcome.IdempotentReplay replayWriteId ->
            replayPreparedBridge journal managerSessionId lifeId life currentObligations previousReview replayWriteId
        | AdmissionOutcome.FreshPrepare preparedPlan ->
            freshPrepareBridge
                journal
                managerSessionId
                lifeId
                locality
                planCompleteDeclared
                currentObligations
                previousReview
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
            let! previousReview = readPreviousReviewView journal life
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

            return!
                materializeAdmission
                    journal
                    managerSessionId
                    lifeId
                    life
                    locality
                    planCompleteDeclared
                    currentObligations
                    previousReview
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
        (acceptedOutputDigest: string option)
        (observedOutputDigest: string)
        : Result<AcceptOutcome, AcceptRejection> =
        match acceptedOutputDigest with
        | Some digest when digest = observedOutputDigest ->
            Ok
                { EnrichedResult = ""
                  NeedsDedicatedEnlist = false
                  NeedsEnsureReview = false }
        | _ -> Error AcceptRejection.OutputDigestMismatch

    let private previousReviewBody (lang: ProviderLanguage) (previousReview: PreviousReviewView option) =
        match previousReview with
        | Some previous when previous.Verdict = ProcessReviewVerdict.Revise ->
            ProviderProse.render
                lang
                MagicTodoSurface.Path.PreviousReviewBody
                (MagicTodoSurface.previousReviewSubs (ProcessReviewVerdict.wire previous.Verdict) previous.ReportText)
        | _ -> ""

    let private enrichAcceptedResult (managerSessionId: SessionId) (isT1Commitment: bool) (rendered: string) =
        if isT1Commitment then
            ManagerNarrative.wrapT1AcceptedResult
                (ProviderProse.documentFor managerSessionId ManagerNarrative.Path.T1Revelation Map.empty)
                rendered
        else
            rendered

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
            let previousBody = previousReviewBody lang bridge.PreviousReview

            let acceptedEpilogue =
                ProviderProse.render lang MagicTodoSurface.Path.ObligationAcceptedEpilogue Map.empty

            let rendered =
                ProviderProse.render
                    lang
                    MagicTodoSurface.Path.ObligationWriteResult
                    (MagicTodoSurface.obligationWriteSubs previousBody acceptedEpilogue)

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

            return
                { EnrichedResult = enrichedResult
                  NeedsDedicatedEnlist = life.Dedicated.IsNone
                  NeedsEnsureReview = checkpoint |> Option.bind (fun value -> value.Concluded) |> Option.isNone }
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
                return acceptIdempotent bridge.AcceptedOutputDigest observedOutputDigest
            else
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
            CanonicalJson.canonicalJson output?output

    let private sessionTextOf (input: obj) =
        if isNull input || isNull input?sessionID then
            ""
        else
            string input?sessionID

    let private languageForSession (sessionText: string) =
        if String.IsNullOrWhiteSpace sessionText then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            SessionProviderLanguage.tryGet (SessionId.create sessionText)
            |> Option.defaultWith ProviderLanguageBinding.readGlobalPreference

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
            | Error(MagicTodoMembrane.PrepareRejection.AwaitingConsumableReview _) ->
                return ObligationLedgerWorkflow.PreparationAttempt.AwaitPreviousReview
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
        | ObligationLedgerWorkflow.PreparationFailure.ReviewWaitFailed reason ->
            fatalInfrastructure sessionText ("await ConsumableReview failed: " + reason)
        | ObligationLedgerWorkflow.PreparationFailure.MissingPendingReview ->
            fatalInfrastructure sessionText "lag-1 wait without pending ConsumableReview (projection inconsistent)"
        | ObligationLedgerWorkflow.PreparationFailure.ReviewDidNotConverge ->
            fatalInfrastructure
                sessionText
                "ConsumableReview wait completed but checkpoint admission still reports the same pending review"

    let private prepareDeferredBridge
        (durable: AgentJournal)
        (snapshots: ISessionSnapshotPort)
        (reviews: ProcessReviewPort)
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
                match SessionSnapshotPort.locateToolCall callId messages with
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
            match! XTraceCapture.captureSessionMessages (Some durable) sessionId priorMessages with
            | Error reason -> fatalInfrastructure sessionText ("XTrace transcript-prefix capture failed: " + reason)
            | Ok() -> ()

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

            let currentPendingReview () =
                let snapshotNow = AgentJournal.snapshot durable

                AgentProjection.tryFind sessionId snapshotNow.AgentProjections
                |> Option.bind (fun session -> session.ManagerLife)
                |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                |> Option.bind (fun mlife ->
                    MagicTodoProjection.tryLife mlife.LifeId snapshotNow.AgentProjections.MagicTodo
                    |> Option.bind MagicTodoProjection.pendingReviewObligation
                    |> Option.map (fun cp -> mlife.LifeId, cp.TodoWriteId))

            let awaitReview (lifeId, writeId) =
                reviews.AwaitConsumableReview durable sessionId lifeId writeId

            let! outcome = ObligationLedgerWorkflow.prepareCheckpoint admitNow currentPendingReview awaitReview

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

    let private ensureReviewPort
        (processReview: ProcessReviewPort option)
        (durable: AgentJournal)
        (prepared: MagicTodoMembrane.PreparedBridge)
        =
        match processReview with
        | None -> Task.FromResult(Error "Magic Todo process review runtime disappeared after before")
        | Some port ->
            port.EnsureReview durable prepared.ManagerSessionId prepared.ManagerLifeId prepared.Prepared.TodoWriteId

    let private applyEnrichedResult (output: obj) (accepted: MagicTodoMembrane.AcceptOutcome) =
        if not (String.IsNullOrEmpty accepted.EnrichedResult) then
            MagicTodoHostCodec.replaceEnrichedResult output accepted.EnrichedResult

    let private acceptResolvedCheckpoint sessionText outcome (output: obj) =
        match outcome with
        | Error(ObligationLedgerWorkflow.AcceptanceFailure.AcceptFailed reason) ->
            fatalInfrastructure sessionText (sprintf "Magic Todo accept invariant failed: %A" reason)
        | Error(ObligationLedgerWorkflow.AcceptanceFailure.ReviewFailed reason) ->
            fatalInfrastructure sessionText ("Magic Todo ensureReview infrastructure failed: " + reason)
        | Ok accepted -> applyEnrichedResult output accepted

    let private acceptAfterPrepare
        (durable: AgentJournal)
        (processReview: ProcessReviewPort option)
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

            let shouldEnsureReview (accepted: MagicTodoMembrane.AcceptOutcome) =
                accepted.NeedsEnsureReview || accepted.NeedsDedicatedEnlist

            let ensureReview (_accepted: MagicTodoMembrane.AcceptOutcome) =
                ensureReviewPort processReview durable prepared

            let! outcome = ObligationLedgerWorkflow.acceptCheckpoint acceptDurably shouldEnsureReview ensureReview

            acceptResolvedCheckpoint sessionText outcome output
        }

    let private runAfterTodo
        (journal: AgentJournal option)
        (processReview: ProcessReviewPort option)
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
                do! acceptAfterPrepare durable processReview sessionText preparedTask output
            finally
                bridges.Remove key |> ignore
        }

    let private runBeforeTodo
        (journal: AgentJournal option)
        (snapshot: ISessionSnapshotPort option)
        (processReview: ProcessReviewPort option)
        (bridges: Dictionary<string, Task<Result<MagicTodoMembrane.PreparedBridge, string>>>)
        (input: obj)
        (output: obj)
        =
        task {
            let durable = requirePort "Magic Todo requires a durable AgentJournal" journal

            let snapshots =
                requirePort "Magic Todo requires the full session snapshot port" snapshot

            let reviews =
                requirePort "Magic Todo requires the process review runtime" processReview

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

                MagicTodoHostCodec.replaceCompatibilityArgs
                    output
                    (obligationsToCompatibilityRows submittedInput.WorkingOn obligations)

                bridges[bridgeKey sessionText callText] <-
                    prepareDeferredBridge
                        durable
                        snapshots
                        reviews
                        sessionId
                        sessionText
                        callId
                        providerInputCanonical
                        submittedInput.PlanComplete
                        obligations
        }

    let create
        (journal: AgentJournal option)
        (snapshot: ISessionSnapshotPort option)
        (processReview: ProcessReviewPort option)
        : HookSet =
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
                    do! runBeforeTodo journal snapshot processReview bridges input output
            }

        let after (input: obj) (output: obj) : Task<unit> =
            task {
                if isTodoTool input "tool" then
                    do! runAfterTodo journal processReview bridges input output
            }

        { Definition = definition
          Before = before
          After = after }
