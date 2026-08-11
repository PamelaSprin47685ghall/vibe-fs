namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoAdmission
open Wanxiangshu.Domain.MagicTodoAfter
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Domain.MagicTodoSurface
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Durable half of the V1 Magic Todo membrane.
///
/// before: localize the persisted ToolPart, validate tagged V2 input, write list
/// bodies, append Prepared, then expose only V1 rows to the builtin executor.
/// after/recovery: prove physical success against that receipt before Accepted.
module MagicTodoMembrane =

    type PreparedBridge =
        { ManagerSessionId: SessionId
          ManagerLifeId: ManagerLifeId
          Prepared: TodoWritePrepared
          PreparedFactRef: EventId
          SettledOld: MagicTodoList
          NormalizedProposal: MagicTodoList
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
        : Result<MagicTodoList, PrepareRejection> =
        match journal.Writer.BlobWriter.Read blobRef with
        | Error reason -> Error(PrepareRejection.BlobRead reason)
        | Ok body when HostDigest.sha256Hex body <> BlobDigest.value expectedDigest ->
            Error(PrepareRejection.BlobDigestMismatch label)
        | Ok body -> MagicTodoListCodec.tryDecode body |> Result.mapError PrepareRejection.BlobDecode

    let private writeList
        (journal: AgentJournal)
        (label: string)
        (items: MagicTodoList)
        : Result<BlobWriteReceipt, PrepareRejection> =
        let body = MagicTodoListCodec.encode items
        let expectedDigest = MagicTodo.listDigest HostDigest.sha256Hex items

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
        (settledOld: MagicTodoList)
        (proposal: MagicTodoList)
        (alreadyAccepted: bool)
        (acceptedOutputDigest: string option)
        =
        { ManagerSessionId = managerSessionId
          ManagerLifeId = lifeId
          Prepared = prepared
          PreparedFactRef = preparedFactRef
          SettledOld = settledOld
          NormalizedProposal = proposal
          CompatibilityRows = toCompatibilityRows ReviewingSinkStrategy.PreserveReviewing proposal
          AlreadyAccepted = alreadyAccepted
          AcceptedOutputDigest = acceptedOutputDigest }

    let prepare
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (locality: MagicTodoLocality.LocalizedToolCall)
        (providerInputCanonical: string)
        (providerInputDigest: string)
        (rawInputs: MagicTodoInputItem list)
        : Result<PreparedBridge, PrepareRejection> =
        if locality.ToolName <> "todowrite" then
            Error(PrepareRejection.UnexpectedToolName locality.ToolName)
        elif locality.InputCanonical <> providerInputCanonical then
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
                        MagicTodoAdmission.admit
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
                            rawInputs

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
                            writeList journal "BaseTodo" preparedPlan.BaseTodo,
                            writeList journal "ProposedTodo" preparedPlan.NormalizedProposed
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
                                        preparedPlan.NormalizedProposed
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

            let dedicatedExists = life.Dedicated.IsSome

            let concludedExists =
                checkpoint |> Option.bind (fun value -> value.Concluded) |> Option.isSome

            match
                MagicTodoAfter.planAccept
                    bridge.Prepared
                    physical
                    bridge.Prepared.ProviderInputDigest
                    observedInputDigest
                    observedOutputDigest
                    bridge.PreparedFactRef
                    dedicatedExists
                    concludedExists
                    None
                    bridge.SettledOld
                    bridge.NormalizedProposal
                    ReviewingSinkStrategy.PreserveReviewing
            with
            | Error rejection -> Error(AcceptRejection.Planner rejection)
            | Ok plan ->
                match
                    AgentJournal.appendMagicTodo
                        (StreamId.Session bridge.ManagerSessionId)
                        None
                        (MagicTodoFact.TodoWriteAccepted plan.Accepted)
                        journal
                with
                | Error failure -> Error(AcceptRejection.JournalAppend(JournalAppendFailure.describe failure))
                | Ok _ ->
                    Ok
                        { EnrichedResult = plan.EnrichedResult
                          NeedsDedicatedEnlist = plan.NeedsDedicatedEnlist
                          NeedsEnsureReview = plan.NeedsEnsureReview }
