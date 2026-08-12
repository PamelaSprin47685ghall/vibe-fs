namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

[<RequireQualifiedAccess>]
type BlessedLifeCompletion =
    | AlreadyCompleted
    | Completed of authorityRoot: AuthorityRootUserMessageId

/// Durable Manager Life transitions that must not be owned by a tool adapter.
module ManagerLifeWorkflow =

    let private appendLifecycle (journal: AgentJournal) (sessionId: SessionId) (fact: ManagerLifecycleFact) =
        AgentJournal.appendManagerLifecycle (StreamId.Session sessionId) fact journal
        |> Result.map (fun _ -> ())
        |> Result.mapError JournalAppendFailure.describe

    /// HumanRoot Birth / Reawakening: WriteBlob → LifeOpened.
    let ensureOpening
        (journal: AgentJournal)
        (sessionId: SessionId)
        (lifeId: ManagerLifeId)
        (openingUserMessageId: PhysicalUserMessageId)
        (rawText: string)
        (openingCursorSequence: int64)
        : Result<unit, string> =
        match journal.WriteBlob rawText with
        | Error error -> Error(sprintf "Life opening blob write failed: %s" error)
        | Ok blob ->
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
            |> Result.mapError (fun failure -> sprintf "Life opening append failed: %s" failure)

    /// GLORY-069 HumanRoot upgrade: WriteBlob → LifeOpened → legacy WorkActivated
    /// (inert decode only; production floor uses effectiveOpeningFloor / T1).
    let ensureMigrated
        (journal: AgentJournal)
        (sessionId: SessionId)
        (lifeId: ManagerLifeId)
        (openingUserMessageId: PhysicalUserMessageId)
        (assignmentText: string)
        (protectedPrefixEndSequence: int64)
        : Result<unit, string> =
        match journal.WriteBlob assignmentText with
        | Error error -> Error(sprintf "Life migration blob write failed: %s" error)
        | Ok blob ->
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
            |> Result.mapError (fun failure -> sprintf "Life migration append failed: %s" failure)
            |> Result.bind (fun () ->
                appendLifecycle
                    journal
                    sessionId
                    (ManagerLifecycleFact.WorkActivated
                        {| SessionId = sessionId
                           LifeId = lifeId
                           ActivationPromptKey = PromptKey.create ""
                           ProtectedPrefixEndSequence = protectedPrefixEndSequence |})
                |> Result.mapError (fun failure -> sprintf "Life migration activation failed: %s" failure))

    /// GLORY-021 legacy: WorkActivated after historical Activation acceptance.
    /// Inert for production floor (TODO-001); BlindPlan nails WorkRecordStart at T1.
    let acceptActivation
        (journal: AgentJournal)
        (sessionId: SessionId)
        (lifeId: ManagerLifeId)
        (activationPromptKey: PromptKey)
        (protectedPrefixEndSequence: int64)
        : Result<unit, string> =
        appendLifecycle
            journal
            sessionId
            (ManagerLifecycleFact.WorkActivated
                {| SessionId = sessionId
                   LifeId = lifeId
                   ActivationPromptKey = activationPromptKey
                   ProtectedPrefixEndSequence = protectedPrefixEndSequence |})
        |> Result.mapError (fun failure -> sprintf "WorkActivated append failed: %s" failure)

    /// GLORY-068/069: AgentOwnerRoot Managers have no HumanRoot-created Life.
    /// On first ending, materialize the migration Life from durable XTrace.
    /// Idempotent: an existing current Life is returned unchanged.
    let ensureMigrationLife (journal: AgentJournal) (sessionId: SessionId) : Result<ManagerLifeId option, string> =
        let snapshot = AgentJournal.snapshot journal

        let openLife (xTrace: XTraceProjectionState option) : Result<ManagerLifeId option, string> =
            match xTrace with
            | None -> Ok None
            | Some xTrace ->
                match xTrace.Opening with
                | None -> Ok None
                | Some opening ->
                    match journal.WriteBlob opening.AssignmentText with
                    | Error error -> Error error
                    | Ok blob ->
                        let lifeId = ManagerLifeId.create (Guid.NewGuid().ToString("N"))

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
                        |> Result.bind (fun () ->
                            appendLifecycle
                                journal
                                sessionId
                                (ManagerLifecycleFact.WorkActivated
                                    {| SessionId = sessionId
                                       LifeId = lifeId
                                       ActivationPromptKey = PromptKey.create ""
                                       ProtectedPrefixEndSequence = XTraceProjection.headSequence xTrace + 1L |}))
                        |> Result.map (fun () -> Some lifeId)

        match AgentProjection.tryFind sessionId snapshot.AgentProjections with
        | None -> Ok None
        | Some session ->
            match session.ManagerLife |> Option.bind (fun lifecycle -> lifecycle.CurrentLife) with
            | Some life -> Ok(Some life.LifeId)
            | None -> openLife session.XTrace

    /// GLORY-062 durable half of the second suicide. Physical terminal publish is
    /// deliberately returned to Infrastructure as a capability effect.
    let completeBlessedLife
        (journal: AgentJournal)
        (sessionId: SessionId)
        (life: LifeProjection)
        (blessing: BlessingEvidence)
        (lastWords: string)
        (providerRun: ProviderRunIdentity)
        : Result<BlessedLifeCompletion, string> =
        match journal.WriteBlob lastWords with
        | Error error -> Error error
        | Ok blob ->
            let snapshot = AgentJournal.snapshot journal

            let alreadyCompleted =
                AgentProjection.tryFind sessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.ManagerLife)
                |> Option.exists (fun lifecycle ->
                    (lifecycle.CurrentLife
                     |> Option.exists (fun current -> current.LifeId = life.LifeId && current.Completed))
                    || lifecycle.CompletedLives
                       |> List.exists (fun completed -> completed.LifeId = life.LifeId))

            if alreadyCompleted then
                Ok BlessedLifeCompletion.AlreadyCompleted
            else
                let terminalRecorded =
                    AgentProjection.tryFind sessionId snapshot.AgentProjections
                    |> Option.bind (fun session -> session.XTrace)
                    |> Option.exists (fun state -> state.Terminal.IsSome)

                appendLifecycle
                    journal
                    sessionId
                    (ManagerLifecycleFact.LifeCompleted
                        {| SessionId = sessionId
                           LifeId = life.LifeId
                           RequestId = blessing.RequestId
                           TerminalRef = blob.BlobRef
                           TerminalDigest = blob.BlobDigest |})
                |> Result.map (fun () ->
                    // Preserve the original error boundary: XTrace terminal capture
                    // is best-effort after the durable LifeCompleted fact.
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

                    let authorityRoot =
                        PromptAuthorityLedger.activeProfile sessionId (AgentJournal.snapshot journal).AgentProjections
                        |> Option.map (fun profile -> profile.AuthorityRootUserMessageId)
                        |> Option.defaultValue (AuthorityRootUserMessageId.create "")

                    BlessedLifeCompletion.Completed authorityRoot)
