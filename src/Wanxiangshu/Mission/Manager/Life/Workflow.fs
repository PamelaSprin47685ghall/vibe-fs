namespace Wanxiangshu.Mission.Manager.Life

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
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
open Wanxiangshu.Mission.Obligation.Todo
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
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type BlessedLifeCompletion =
    | AlreadyCompleted
    | Completed of authorityRoot: AuthorityRootUserMessageId

/// Durable Manager Life transitions that must not be owned by a tool adapter.
module ManagerLifeWorkflow =

    let private appendLifecycle (journal: AgentJournal) (sessionId: SessionId) (fact: ManagerLifecycleFact) =
        task {
            match! AgentJournal.appendManagerLifecycle (StreamId.Session sessionId) fact journal with
            | Ok _ -> return Ok()
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    /// HumanRoot Birth / Reawakening: WriteBlob → LifeOpened.
    let ensureOpening
        (journal: AgentJournal)
        (sessionId: SessionId)
        (lifeId: ManagerLifeId)
        (openingUserMessageId: PhysicalUserMessageId)
        (rawText: string)
        (openingCursorSequence: int64)
        : Task<Result<unit, string>> =
        task {
            match! journal.WriteBlob rawText with
            | Error error -> return Error(sprintf "Life opening blob write failed: %s" error)
            | Ok blob ->
                match!
                    appendLifecycle
                        journal
                        sessionId
                        (ManagerLifecycleFact.LifeOpened
                            {| SessionId = sessionId
                               LifeId = lifeId
                               OpeningUserMessageId = openingUserMessageId
                               OpeningTextRef = blob.BlobRef
                               OpeningTextDigest = blob.BlobDigest
                               OpeningCursorSequence = openingCursorSequence |})
                with
                | Ok() -> return Ok()
                | Error failure -> return Error(sprintf "Life opening append failed: %s" failure)
        }

    /// GLORY-069 HumanRoot upgrade: WriteBlob → LifeOpened → legacy WorkActivated
    /// (inert decode only; production floor uses effectiveOpeningFloor / T1).
    let ensureMigrated
        (journal: AgentJournal)
        (sessionId: SessionId)
        (lifeId: ManagerLifeId)
        (openingUserMessageId: PhysicalUserMessageId)
        (assignmentText: string)
        (protectedPrefixEndSequence: int64)
        : Task<Result<unit, string>> =
        task {
            match! journal.WriteBlob assignmentText with
            | Error error -> return Error(sprintf "Life migration blob write failed: %s" error)
            | Ok blob ->
                match!
                    appendLifecycle
                        journal
                        sessionId
                        (ManagerLifecycleFact.LifeOpened
                            {| SessionId = sessionId
                               LifeId = lifeId
                               OpeningUserMessageId = openingUserMessageId
                               OpeningTextRef = blob.BlobRef
                               OpeningTextDigest = blob.BlobDigest
                               OpeningCursorSequence = 0L |})
                with
                | Error failure -> return Error(sprintf "Life migration append failed: %s" failure)
                | Ok() ->
                    match!
                        appendLifecycle
                            journal
                            sessionId
                            (ManagerLifecycleFact.WorkActivated
                                {| SessionId = sessionId
                                   LifeId = lifeId
                                   ActivationPromptKey = PromptKey.create ""
                                   ProtectedPrefixEndSequence = protectedPrefixEndSequence |})
                    with
                    | Ok() -> return Ok()
                    | Error failure -> return Error(sprintf "Life migration activation failed: %s" failure)
        }

    /// GLORY-021 legacy: WorkActivated after historical Activation acceptance.
    /// Inert for production floor (TODO-001); BlindPlan nails WorkRecordStart at T1.
    let acceptActivation
        (journal: AgentJournal)
        (sessionId: SessionId)
        (lifeId: ManagerLifeId)
        (activationPromptKey: PromptKey)
        (protectedPrefixEndSequence: int64)
        : Task<Result<unit, string>> =
        task {
            match!
                appendLifecycle
                    journal
                    sessionId
                    (ManagerLifecycleFact.WorkActivated
                        {| SessionId = sessionId
                           LifeId = lifeId
                           ActivationPromptKey = activationPromptKey
                           ProtectedPrefixEndSequence = protectedPrefixEndSequence |})
            with
            | Ok() -> return Ok()
            | Error failure -> return Error(sprintf "WorkActivated append failed: %s" failure)
        }

    let private materializeInitialAgentOwnerLife
        (journal: AgentJournal)
        (sessionId: SessionId)
        (evidence: InitialAgentOwnerMigrationEvidence)
        : Task<Result<LifeProjection option, string>> =
        taskResult {
            let xTrace = InitialAgentOwnerMigrationEvidence.xTrace evidence

            let opening =
                match xTrace.Opening with
                | Some value -> Ok value
                | None -> Error "AgentOwnerRoot migration requires OpeningMaterial"

            let! opening = opening
            let! blob = journal.WriteBlob opening.AssignmentText
            let lifeId = ManagerLifeId.create (Guid.NewGuid().ToString("N"))

            do!
                appendLifecycle
                    journal
                    sessionId
                    (ManagerLifecycleFact.LifeOpened
                        {| SessionId = sessionId
                           LifeId = lifeId
                           OpeningUserMessageId = PhysicalUserMessageId.create (SessionId.value sessionId)
                           OpeningTextRef = blob.BlobRef
                           OpeningTextDigest = blob.BlobDigest
                           OpeningCursorSequence = 0L |})

            do!
                appendLifecycle
                    journal
                    sessionId
                    (ManagerLifecycleFact.WorkActivated
                        {| SessionId = sessionId
                           LifeId = lifeId
                           ActivationPromptKey = PromptKey.create ""
                           ProtectedPrefixEndSequence = XTraceProjection.headSequence xTrace + 1L |})

            return
                AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
                |> Option.bind (fun session -> session.ManagerLife)
                |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
        }

    /// FINALITY-022 admission owner for ending-time Life lookup.
    /// Existing Life wins; an AgentOwnerRoot may materialize exactly one migration
    /// Life before any completed-Life history exists; otherwise no Life is admitted.
    let ensureEndingLife (journal: AgentJournal) (sessionId: SessionId) : Task<Result<LifeProjection option, string>> =
        let snapshot = AgentJournal.snapshot journal
        let session = AgentProjection.tryFind sessionId snapshot.AgentProjections

        let lifecycle =
            session
            |> Option.bind (fun value -> value.ManagerLife)
            |> Option.defaultValue ManagerLifecycleProjection.empty

        let profile =
            PromptAuthorityLedger.activeProfile sessionId snapshot.AgentProjections

        let xTrace = session |> Option.bind (fun value -> value.XTrace)

        match ManagerLifeAdmission.ending lifecycle profile xTrace with
        | EndingLifeAdmission.ExistingLife life -> Task.FromResult(Ok(Some life))
        | EndingLifeAdmission.NoLife -> Task.FromResult(Ok None)
        | EndingLifeAdmission.InitialAgentOwnerMigration evidence ->
            materializeInitialAgentOwnerLife journal sessionId evidence

    let private captureTerminalIfMissing
        (journal: AgentJournal)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (blob: BlobWriteReceipt)
        (terminalRecorded: bool) =
        if not terminalRecorded then
            AgentJournal.appendAgent
                (StreamId.Session sessionId)
                (Some providerRun)
                (CompanionFact.TerminalOutputCaptured
                    {| SessionId = sessionId
                       TextRef = blob.BlobRef
                       TextDigest = blob.BlobDigest
                       ProviderRun = providerRun |})
                journal
            |> ignore

    let private captureLastWordsIfPresent
        (journal: AgentJournal)
        (sessionId: SessionId)
        (lastWords: string)
        (providerRun: ProviderRunIdentity)
        (blob: BlobWriteReceipt) =
        if not (String.IsNullOrWhiteSpace lastWords) then
            XTraceCapture.captureLastWords
                (Some journal)
                sessionId
                blob.BlobRef
                blob.BlobDigest
                providerRun
        else
            Task.FromResult()

    let private completeFreshBlessedLife
        (journal: AgentJournal)
        (sessionId: SessionId)
        (life: LifeProjection)
        (blessing: BlessingEvidence)
        (lastWords: string)
        (providerRun: ProviderRunIdentity)
        (blob: BlobWriteReceipt) =
        task {
            let snapshot = AgentJournal.snapshot journal
            let terminalRecorded =
                AgentProjection.tryFind sessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.XTrace)
                |> Option.exists (fun state -> state.Terminal.IsSome)

            match!
                appendLifecycle
                    journal
                    sessionId
                    (ManagerLifecycleFact.LifeCompleted
                        {| SessionId = sessionId
                           LifeId = life.LifeId
                           RequestId = blessing.RequestId
                           TerminalRef = blob.BlobRef
                           TerminalDigest = blob.BlobDigest |})
            with
            | Error error -> return Error error
            | Ok() ->
                captureTerminalIfMissing journal sessionId providerRun blob terminalRecorded
                do! captureLastWordsIfPresent journal sessionId lastWords providerRun blob

                let authorityRoot =
                    PromptAuthorityLedger.activeProfile sessionId (AgentJournal.snapshot journal).AgentProjections
                    |> Option.map (fun profile -> profile.AuthorityRootUserMessageId)
                    |> Option.defaultValue (AuthorityRootUserMessageId.create "")

                return Ok(BlessedLifeCompletion.Completed authorityRoot)
        }

    let private completeWithBlessingBlob
        (journal: AgentJournal)
        (sessionId: SessionId)
        (life: LifeProjection)
        (blessing: BlessingEvidence)
        (lastWords: string)
        (providerRun: ProviderRunIdentity)
        (blob: BlobWriteReceipt) =
        let snapshot = AgentJournal.snapshot journal
        let alreadyCompleted =
            AgentProjection.tryFind sessionId snapshot.AgentProjections
            |> Option.bind (fun session -> session.ManagerLife)
            |> Option.exists (fun lifecycle ->
                (lifecycle.CurrentLife |> Option.exists (fun current -> current.LifeId = life.LifeId && current.Completed))
                || lifecycle.CompletedLives |> List.exists (fun completed -> completed.LifeId = life.LifeId))

        match alreadyCompleted with
        | true -> Task.FromResult(Ok BlessedLifeCompletion.AlreadyCompleted)
        | false -> completeFreshBlessedLife journal sessionId life blessing lastWords providerRun blob

    /// GLORY-062 durable half of the second suicide. Physical terminal publish is
    /// deliberately returned to Infrastructure as a capability effect.
    let completeBlessedLife
        (journal: AgentJournal)
        (sessionId: SessionId)
        (life: LifeProjection)
        (blessing: BlessingEvidence)
        (lastWords: string)
        (providerRun: ProviderRunIdentity)
        : Task<Result<BlessedLifeCompletion, string>> =
        task {
            match! journal.WriteBlob lastWords with
            | Error error -> return Error error
            | Ok blob -> return! completeWithBlessingBlob journal sessionId life blessing lastWords providerRun blob
        }
