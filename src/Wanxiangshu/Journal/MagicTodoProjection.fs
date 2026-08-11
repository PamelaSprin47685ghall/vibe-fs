namespace Wanxiangshu.Journal

open Wanxiangshu.Domain
open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// MagicTodoProjection — canonical todo list + lag-1 obligations from facts.
///
/// Protocol SSOT #3. No TodoStage / HasPendingReview bool: pending review is
/// `Accepted exists ∧ matching Concluded missing`. Settlement applies only when
/// ConsumableReview (TodoReviewConcluded) is consumed by the next prepare /
/// suicide drain.
module MagicTodoProjection =

    type CheckpointRecord =
        { ManagerSessionId: SessionId
          TodoWriteId: TodoWriteId
          ToolCallId: ToolCallId
          ToolPartOrdinal: int
          BaseTodoDigest: BlobDigest
          ProposedTodoDigest: BlobDigest
          BaseTodoRef: BlobRef
          ProposedTodoRef: BlobRef
          ProviderInputDigest: string
          ReviewFrontier: XTraceCursor
          SemanticVersion: string
          PreparedFactRef: EventId
          InputDigest: string option
          OutputDigest: string option
          Accepted: bool
          Assignment: TodoProcessReviewAssigned option
          Concluded: TodoReviewConcluded option }

    type DedicatedReviewerState =
        { DedicatedReviewerId: DedicatedReviewerId
          ReviewerSessionId: SessionId }

    /// Per-Life Magic Todo derived view.
    type LifeMagicTodoState =
        {
            LifeId: ManagerLifeId
            /// Settled current (Ck for the next prepare). None means normal new Life.
            SettledCurrentRef: (BlobRef * BlobDigest) option
            /// Accepted chain in order (lag-1 desired cutoff source).
            AcceptedOrder: TodoWriteId list
            Checkpoints: Map<string, CheckpointRecord>
            Dedicated: DedicatedReviewerState option
            /// Blob list bodies live in the blob store; projection keeps their locator and ids.
            LegacySeed: (BlobRef * BlobDigest * TodoItemId list) option
        }

    type MagicTodoProjectionState =
        { ByLife: Map<string, LifeMagicTodoState> }

    [<RequireQualifiedAccess>]
    type MagicTodoFoldRejection =
        | LifeMismatch of expected: string * actual: string
        | PreparedMissingForAccept of todoWriteId: string
        | OutstandingReviewBeforePrepare of pendingTodoWriteId: string
        | IdentityCorruption of field: string
        | AssignmentWithoutAccepted of todoWriteId: string
        | ConcludedWithoutAccepted of todoWriteId: string
        | LegacySeedAfterCheckpoint
        | DedicatedMissingForAssign
        | DedicatedMissingForReplace

    let empty: MagicTodoProjectionState = { ByLife = Map.empty }

    let private lifeKey (lifeId: ManagerLifeId) = ManagerLifeId.value lifeId

    let private writeKey (writeId: TodoWriteId) = TodoWriteId.value writeId

    let emptyLife (lifeId: ManagerLifeId) : LifeMagicTodoState =
        { LifeId = lifeId
          SettledCurrentRef = None
          AcceptedOrder = []
          Checkpoints = Map.empty
          Dedicated = None
          LegacySeed = None }

    let tryLife (lifeId: ManagerLifeId) (state: MagicTodoProjectionState) : LifeMagicTodoState option =
        Map.tryFind (lifeKey lifeId) state.ByLife

    let ensureLife
        (lifeId: ManagerLifeId)
        (state: MagicTodoProjectionState)
        : LifeMagicTodoState * MagicTodoProjectionState =
        match tryLife lifeId state with
        | Some life -> life, state
        | None ->
            let life = emptyLife lifeId
            life, { ByLife = Map.add (lifeKey lifeId) life state.ByLife }

    let private putLife (life: LifeMagicTodoState) (state: MagicTodoProjectionState) =
        { ByLife = Map.add (lifeKey life.LifeId) life state.ByLife }

    /// Pending process-review obligation: Accepted ∧ ¬Concluded.
    let pendingReviewObligation (life: LifeMagicTodoState) : CheckpointRecord option =
        life.AcceptedOrder
        |> List.tryFind (fun writeId ->
            match Map.tryFind (writeKey writeId) life.Checkpoints with
            | Some cp when cp.Accepted && cp.Concluded.IsNone -> true
            | _ -> false)
        |> Option.bind (fun writeId -> Map.tryFind (writeKey writeId) life.Checkpoints)

    /// Next todowrite / suicide may consume only when Concluded exists for latest Accepted.
    let consumablePreviousReview (life: LifeMagicTodoState) : TodoReviewConcluded option =
        match life.AcceptedOrder |> List.tryLast with
        | None -> None
        | Some writeId ->
            match Map.tryFind (writeKey writeId) life.Checkpoints with
            | Some { Concluded = Some concluded } -> Some concluded
            | Some { Accepted = true; Concluded = None } -> None
            | _ -> None

    /// True when a new TodoWritePrepared may proceed past lag-1 await.
    let mayAdmitNewCheckpoint (life: LifeMagicTodoState) : Result<unit, MagicTodoReject> =
        match pendingReviewObligation life with
        | None -> Ok()
        | Some cp -> Error(MagicTodoReject.AwaitingConsumableReview(TodoWriteId.value cp.TodoWriteId))

    /// Desired lag-1 cutoff from Accepted chain (no Requested fact).
    let desiredLag1 (life: LifeMagicTodoState) : TodoWriteId option =
        MagicTodo.desiredLag1Cutoff life.AcceptedOrder

    let materializeSettledCurrent
        (decodeList: BlobRef -> BlobDigest -> MagicTodoList)
        (life: LifeMagicTodoState)
        : MagicTodoList =
        match life.SettledCurrentRef with
        | Some(blobRef, digest) -> decodeList blobRef digest
        | None -> []

    let foldPrepared
        (preparedFactRef: EventId)
        (payload: TodoWritePrepared)
        (state: MagicTodoProjectionState)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state
        let key = writeKey payload.TodoWriteId

        match Map.tryFind key life.Checkpoints with
        | Some existing ->
            if existing.ManagerSessionId <> payload.ManagerSessionId then
                Error(MagicTodoFoldRejection.IdentityCorruption "ManagerSessionId")
            elif existing.ToolCallId <> payload.ToolCallId then
                Error(MagicTodoFoldRejection.IdentityCorruption "ToolCallId")
            elif existing.ProviderInputDigest <> payload.ProviderInputDigest then
                Error(MagicTodoFoldRejection.IdentityCorruption "ProviderInputDigest")
            elif existing.BaseTodoDigest <> payload.BaseTodoDigest then
                Error(MagicTodoFoldRejection.IdentityCorruption "BaseTodoDigest")
            elif existing.ProposedTodoDigest <> payload.ProposedTodoDigest then
                Error(MagicTodoFoldRejection.IdentityCorruption "ProposedTodoDigest")
            elif existing.BaseTodoRef <> payload.BaseTodoRef then
                Error(MagicTodoFoldRejection.IdentityCorruption "BaseTodoRef")
            elif existing.ProposedTodoRef <> payload.ProposedTodoRef then
                Error(MagicTodoFoldRejection.IdentityCorruption "ProposedTodoRef")
            elif existing.ToolPartOrdinal <> payload.ToolPartOrdinal then
                Error(MagicTodoFoldRejection.IdentityCorruption "ToolPartOrdinal")
            elif existing.ReviewFrontier <> payload.ReviewFrontier then
                Error(MagicTodoFoldRejection.IdentityCorruption "ReviewFrontier")
            elif existing.SemanticVersion <> payload.SemanticVersion then
                Error(MagicTodoFoldRejection.IdentityCorruption "SemanticVersion")
            else
                Ok state
        | None ->
            match pendingReviewObligation life with
            | Some pending ->
                Error(MagicTodoFoldRejection.OutstandingReviewBeforePrepare(TodoWriteId.value pending.TodoWriteId))
            | None ->
                let cp =
                    { ManagerSessionId = payload.ManagerSessionId
                      TodoWriteId = payload.TodoWriteId
                      ToolCallId = payload.ToolCallId
                      ToolPartOrdinal = payload.ToolPartOrdinal
                      BaseTodoDigest = payload.BaseTodoDigest
                      ProposedTodoDigest = payload.ProposedTodoDigest
                      BaseTodoRef = payload.BaseTodoRef
                      ProposedTodoRef = payload.ProposedTodoRef
                      ProviderInputDigest = payload.ProviderInputDigest
                      ReviewFrontier = payload.ReviewFrontier
                      SemanticVersion = payload.SemanticVersion
                      PreparedFactRef = preparedFactRef
                      InputDigest = None
                      OutputDigest = None
                      Accepted = false
                      Assignment = None
                      Concluded = None }

                Ok(
                    putLife
                        { life with
                            Checkpoints = Map.add key cp life.Checkpoints }
                        state
                )

    let foldAccepted
        (payload: TodoWriteAccepted)
        (state: MagicTodoProjectionState)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state
        let key = writeKey payload.TodoWriteId

        match Map.tryFind key life.Checkpoints with
        | None -> Error(MagicTodoFoldRejection.PreparedMissingForAccept key)
        | Some cp when cp.PreparedFactRef <> payload.PreparedFactRef ->
            Error(MagicTodoFoldRejection.IdentityCorruption "PreparedFactRef")
        | Some cp when cp.ToolCallId <> payload.ToolCallId ->
            Error(MagicTodoFoldRejection.IdentityCorruption "ToolCallId")
        | Some cp when cp.ProviderInputDigest <> payload.InputDigest ->
            Error(MagicTodoFoldRejection.IdentityCorruption "InputDigest")
        | Some cp when cp.SemanticVersion <> payload.SemanticVersion ->
            Error(MagicTodoFoldRejection.IdentityCorruption "SemanticVersion")
        | Some cp when cp.Accepted ->
            match cp.InputDigest, cp.OutputDigest with
            | Some inputDigest, Some outputDigest when
                inputDigest = payload.InputDigest && outputDigest = payload.OutputDigest
                ->
                Ok state
            | Some _, Some _ -> Error(MagicTodoFoldRejection.IdentityCorruption "AcceptedDigest")
            | _ -> Error(MagicTodoFoldRejection.IdentityCorruption "AcceptedState")
        | Some cp ->
            let cp =
                { cp with
                    Accepted = true
                    InputDigest = Some payload.InputDigest
                    OutputDigest = Some payload.OutputDigest }

            let acceptedOrder =
                if List.exists (fun id -> TodoWriteId.value id = key) life.AcceptedOrder then
                    life.AcceptedOrder
                else
                    life.AcceptedOrder @ [ payload.TodoWriteId ]

            Ok(
                putLife
                    { life with
                        Checkpoints = Map.add key cp life.Checkpoints
                        AcceptedOrder = acceptedOrder }
                    state
            )

    let foldAssigned
        (payload: TodoProcessReviewAssigned)
        (state: MagicTodoProjectionState)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state
        let key = writeKey payload.TodoWriteId

        match Map.tryFind key life.Checkpoints with
        | None
        | Some { Accepted = false } -> Error(MagicTodoFoldRejection.AssignmentWithoutAccepted key)
        | Some cp ->
            match life.Dedicated with
            | None -> Error MagicTodoFoldRejection.DedicatedMissingForAssign
            | Some dedicated when
                dedicated.DedicatedReviewerId <> payload.DedicatedReviewerId
                || dedicated.ReviewerSessionId <> payload.ReviewerSessionId
                ->
                Error(MagicTodoFoldRejection.IdentityCorruption "DedicatedReviewer")
            | Some _ ->
                match cp.Assignment with
                | Some existing when existing = payload -> Ok state
                | Some _ -> Error(MagicTodoFoldRejection.IdentityCorruption "TodoProcessReviewAssigned")
                | None ->
                    Ok(
                        putLife
                            { life with
                                Checkpoints = Map.add key { cp with Assignment = Some payload } life.Checkpoints }
                            state
                    )

    let foldConcluded
        (payload: TodoReviewConcluded)
        (state: MagicTodoProjectionState)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state
        let key = writeKey payload.TodoWriteId

        match Map.tryFind key life.Checkpoints with
        | None
        | Some { Accepted = false } -> Error(MagicTodoFoldRejection.ConcludedWithoutAccepted key)
        | Some { Concluded = Some existing } ->
            if existing = payload then
                Ok state
            else
                Error(MagicTodoFoldRejection.IdentityCorruption "TodoReviewConcluded")
        | Some cp ->
            match cp.Assignment with
            | None -> Error(MagicTodoFoldRejection.AssignmentWithoutAccepted key)
            | Some assignment ->
                if
                    assignment.TodoReviewId <> payload.TodoReviewId
                    || assignment.DedicatedReviewerId <> payload.DedicatedReviewerId
                    || assignment.ReviewerSessionId <> payload.ReviewerSessionId
                then
                    Error(MagicTodoFoldRejection.IdentityCorruption "TodoReviewAssignment")
                else
                    let cp = { cp with Concluded = Some payload }

                    Ok(
                        putLife
                            { life with
                                Checkpoints = Map.add key cp life.Checkpoints
                                SettledCurrentRef = Some(payload.SettledTodoRef, payload.SettledTodoDigest) }
                            state
                    )

    let foldDedicatedEnlisted (payload: DedicatedTodoReviewerEnlisted) (state: MagicTodoProjectionState) =
        let life, state = ensureLife payload.ManagerLifeId state

        match life.Dedicated with
        | Some d when
            d.DedicatedReviewerId = payload.DedicatedReviewerId
            && d.ReviewerSessionId = payload.ReviewerSessionId
            ->
            Ok state
        | Some d when d.DedicatedReviewerId = payload.DedicatedReviewerId ->
            // Same logical id, physical session change must go through Replaced.
            Error(MagicTodoFoldRejection.IdentityCorruption "ReviewerSessionId")
        | Some _ -> Error(MagicTodoFoldRejection.IdentityCorruption "DedicatedReviewerId")
        | None ->
            Ok(
                putLife
                    { life with
                        Dedicated =
                            Some
                                { DedicatedReviewerId = payload.DedicatedReviewerId
                                  ReviewerSessionId = payload.ReviewerSessionId } }
                    state
            )

    let foldDedicatedReplaced (payload: DedicatedTodoReviewerReplaced) (state: MagicTodoProjectionState) =
        let life, state = ensureLife payload.ManagerLifeId state

        match life.Dedicated with
        | None -> Error MagicTodoFoldRejection.DedicatedMissingForReplace
        | Some d when d.DedicatedReviewerId <> payload.DedicatedReviewerId ->
            Error(MagicTodoFoldRejection.IdentityCorruption "DedicatedReviewerId")
        | Some d when d.ReviewerSessionId <> payload.OldSessionId ->
            Error(MagicTodoFoldRejection.IdentityCorruption "OldSessionId")
        | Some _ ->
            Ok(
                putLife
                    { life with
                        Dedicated =
                            Some
                                { DedicatedReviewerId = payload.DedicatedReviewerId
                                  ReviewerSessionId = payload.NewSessionId } }
                    state
            )

    let foldLegacySeed
        (payload: LegacyTodoSeedAdopted)
        (state: MagicTodoProjectionState)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state

        match life.LegacySeed with
        | Some(seedRef, seedDigest, seedItemIds) when
            seedRef = payload.SeedTodoRef
            && seedDigest = payload.SeedTodoDigest
            && seedItemIds = payload.SeedItemIds
            ->
            Ok state
        | Some _ -> Error(MagicTodoFoldRejection.IdentityCorruption "LegacyTodoSeed")
        | None when not (Map.isEmpty life.Checkpoints) -> Error MagicTodoFoldRejection.LegacySeedAfterCheckpoint
        | None ->
            Ok(
                putLife
                    { life with
                        SettledCurrentRef = Some(payload.SeedTodoRef, payload.SeedTodoDigest)
                        LegacySeed = Some(payload.SeedTodoRef, payload.SeedTodoDigest, payload.SeedItemIds) }
                    state
            )

    /// Dispatch one Magic Todo fact. PrefixRebaseCommittedV2 is owned by
    /// PrefixEpochProjection once wired — ignored here (no todo-list effect).
    let fold
        (envelopeEventId: EventId)
        (state: MagicTodoProjectionState)
        (fact: MagicTodoFact)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        match fact with
        | MagicTodoFact.TodoWritePrepared payload -> foldPrepared envelopeEventId payload state
        | MagicTodoFact.TodoWriteAccepted payload -> foldAccepted payload state
        | MagicTodoFact.TodoProcessReviewAssigned payload -> foldAssigned payload state
        | MagicTodoFact.TodoReviewConcluded payload -> foldConcluded payload state
        | MagicTodoFact.DedicatedTodoReviewerEnlisted payload -> foldDedicatedEnlisted payload state
        | MagicTodoFact.DedicatedTodoReviewerReplaced payload -> foldDedicatedReplaced payload state
        | MagicTodoFact.LegacyTodoSeedAdopted payload -> foldLegacySeed payload state
        | MagicTodoFact.PrefixRebaseCommittedV2 _ -> Ok state
