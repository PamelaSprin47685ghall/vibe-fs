namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Domain
open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// MagicTodoProjection — canonical todo list + lag-1 obligations from facts.
///
/// Protocol SSOT #3. No TodoStage / HasPendingReview bool: pending review is
/// `Accepted exists ∧ matching Concluded missing`. Accepted immediately owns
/// CurrentObligations; Concluded only closes the lag-1 review obligation.
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
          PlanCompleteDeclared: bool
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
            /// Canonical provider account: the latest Accepted checkpoint's Submitted account.
            /// None means a normal new Life before T1 (or no legacy seed).
            CurrentObligationsRef: (BlobRef * BlobDigest) option
            /// O(1) event-integral locators. These name facts that already
            /// happened; none is a workflow program counter.
            FirstAcceptedCheckpoint: TodoWriteId option
            LatestAcceptedCheckpoint: TodoWriteId option
            PendingReviewCheckpoint: TodoWriteId option
            FirstPlanCommitment: TodoWriteId option
            LatestCommittedCheckpoint: TodoWriteId option
            PreviousCommittedCheckpoint: TodoWriteId option
            Checkpoints: Map<string, CheckpointRecord>
            Dedicated: DedicatedReviewerState option
            /// Upgrade-only canonical obligation seed locator.
            LegacySeed: (BlobRef * BlobDigest) option
        }

    type MagicTodoProjectionState =
        { ByLife: Map<string, LifeMagicTodoState>
          /// O(1) reverse locator for reviewer-session authority lookup.
          ReviewerLifeBySession: Map<string, ManagerLifeId> }

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

    let empty: MagicTodoProjectionState =
        { ByLife = Map.empty
          ReviewerLifeBySession = Map.empty }

    let private lifeKey (lifeId: ManagerLifeId) = ManagerLifeId.value lifeId

    let private writeKey (writeId: TodoWriteId) = TodoWriteId.value writeId

    let private reviewerSessionKey (sessionId: SessionId) = SessionId.value sessionId

    let emptyLife (lifeId: ManagerLifeId) : LifeMagicTodoState =
        { LifeId = lifeId
          CurrentObligationsRef = None
          FirstAcceptedCheckpoint = None
          LatestAcceptedCheckpoint = None
          PendingReviewCheckpoint = None
          FirstPlanCommitment = None
          LatestCommittedCheckpoint = None
          PreviousCommittedCheckpoint = None
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
            life, { state with ByLife = Map.add (lifeKey lifeId) life state.ByLife }

    let private putLife (life: LifeMagicTodoState) (state: MagicTodoProjectionState) =
        { state with ByLife = Map.add (lifeKey life.LifeId) life state.ByLife }

    let isPlanCommitted (life: LifeMagicTodoState) : bool = life.FirstPlanCommitment.IsSome

    let isFirstAcceptedCheckpoint (life: LifeMagicTodoState) (writeId: TodoWriteId) : bool =
        life.FirstAcceptedCheckpoint = Some writeId

    /// Pending process-review obligation: the protocol admits at most one.
    let pendingReviewObligation (life: LifeMagicTodoState) : CheckpointRecord option =
        life.PendingReviewCheckpoint
        |> Option.bind (fun writeId -> Map.tryFind (writeKey writeId) life.Checkpoints)

    /// Next todowrite / suicide may consume only when Concluded exists for latest Accepted.
    let consumablePreviousReview (life: LifeMagicTodoState) : TodoReviewConcluded option =
        life.LatestAcceptedCheckpoint
        |> Option.bind (fun writeId -> Map.tryFind (writeKey writeId) life.Checkpoints)
        |> Option.bind (fun checkpoint -> checkpoint.Concluded)

    /// True when a new TodoWritePrepared may proceed past lag-1 await.
    /// `AwaitingConsumableReview` is the wait signal for deferred prepare / suicide
    /// drain (TODO-006), not a provider-visible reject.
    let mayAdmitNewCheckpoint (life: LifeMagicTodoState) : Result<unit, MagicTodoReject> =
        match pendingReviewObligation life with
        | None -> Ok()
        | Some cp -> Error(MagicTodoReject.AwaitingConsumableReview(TodoWriteId.value cp.TodoWriteId))

    /// REVIEW-013: typed process-review authority for a dedicated reviewer session.
    /// Presence of Accepted ∧ Assigned ∧ ¬Concluded on this reviewer is RequestKind
    /// TodoProcessReview — not a pendingChallenge guess.
    let pendingProcessReviewForReviewer
        (reviewerSessionId: SessionId)
        (state: MagicTodoProjectionState)
        : CheckpointRecord option =
        state.ReviewerLifeBySession
        |> Map.tryFind (reviewerSessionKey reviewerSessionId)
        |> Option.bind (fun lifeId -> tryLife lifeId state)
        |> Option.bind pendingReviewObligation

    /// Desired committed lag-1 cutoff. Pre-T1 planning checkpoints never enter it.
    let desiredLag1 (life: LifeMagicTodoState) : TodoWriteId option = life.PreviousCommittedCheckpoint

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
            elif existing.PlanCompleteDeclared <> payload.PlanCompleteDeclared then
                Error(MagicTodoFoldRejection.IdentityCorruption "PlanCompleteDeclared")
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
                      PlanCompleteDeclared = payload.PlanCompleteDeclared
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

            let firstAccepted =
                match life.FirstAcceptedCheckpoint with
                | Some existing -> Some existing
                | None -> Some payload.TodoWriteId

            let firstCommitment, previousCommitted, latestCommitted =
                match life.FirstPlanCommitment with
                | None when cp.PlanCompleteDeclared ->
                    Some payload.TodoWriteId, None, Some payload.TodoWriteId
                | None -> None, None, None
                | Some first ->
                    Some first, life.LatestCommittedCheckpoint, Some payload.TodoWriteId

            Ok(
                putLife
                    { life with
                        Checkpoints = Map.add key cp life.Checkpoints
                        FirstAcceptedCheckpoint = firstAccepted
                        LatestAcceptedCheckpoint = Some payload.TodoWriteId
                        PendingReviewCheckpoint = Some payload.TodoWriteId
                        FirstPlanCommitment = firstCommitment
                        PreviousCommittedCheckpoint = previousCommitted
                        LatestCommittedCheckpoint = latestCommitted
                        CurrentObligationsRef = Some(cp.ProposedTodoRef, cp.ProposedTodoDigest) }
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

                    if life.PendingReviewCheckpoint <> Some payload.TodoWriteId then
                        Error(MagicTodoFoldRejection.IdentityCorruption "PendingReviewCheckpoint")
                    else
                        Ok(
                            putLife
                                { life with
                                    Checkpoints = Map.add key cp life.Checkpoints
                                    PendingReviewCheckpoint = None }
                                state
                        )

    let foldDedicatedEnlisted (payload: DedicatedTodoReviewerEnlisted) (state: MagicTodoProjectionState) =
        let life, state = ensureLife payload.ManagerLifeId state
        let reviewerKey = reviewerSessionKey payload.ReviewerSessionId

        match Map.tryFind reviewerKey state.ReviewerLifeBySession with
        | Some indexedLife when indexedLife <> payload.ManagerLifeId ->
            Error(MagicTodoFoldRejection.IdentityCorruption "ReviewerLifeBySession")
        | _ ->
            let indexedState =
                { state with
                    ReviewerLifeBySession =
                        Map.add reviewerKey payload.ManagerLifeId state.ReviewerLifeBySession }

            match life.Dedicated with
            | Some d when
                d.DedicatedReviewerId = payload.DedicatedReviewerId
                && d.ReviewerSessionId = payload.ReviewerSessionId
                ->
                Ok indexedState
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
                        indexedState
                )

    let foldDedicatedReplaced (payload: DedicatedTodoReviewerReplaced) (state: MagicTodoProjectionState) =
        let life, state = ensureLife payload.ManagerLifeId state
        let oldKey = reviewerSessionKey payload.OldSessionId
        let newKey = reviewerSessionKey payload.NewSessionId

        match life.Dedicated with
        | None -> Error MagicTodoFoldRejection.DedicatedMissingForReplace
        | Some d when d.DedicatedReviewerId <> payload.DedicatedReviewerId ->
            Error(MagicTodoFoldRejection.IdentityCorruption "DedicatedReviewerId")
        | Some d when d.ReviewerSessionId <> payload.OldSessionId ->
            Error(MagicTodoFoldRejection.IdentityCorruption "OldSessionId")
        | Some _ ->
            match Map.tryFind oldKey state.ReviewerLifeBySession with
            | Some indexedLife when indexedLife <> payload.ManagerLifeId ->
                Error(MagicTodoFoldRejection.IdentityCorruption "OldReviewerLifeBySession")
            | _ ->
                match Map.tryFind newKey state.ReviewerLifeBySession with
                | Some indexedLife when indexedLife <> payload.ManagerLifeId ->
                    Error(MagicTodoFoldRejection.IdentityCorruption "NewReviewerLifeBySession")
                | _ ->
                    let indexedState =
                        { state with
                            ReviewerLifeBySession =
                                state.ReviewerLifeBySession
                                |> Map.remove oldKey
                                |> Map.add newKey payload.ManagerLifeId }

                    Ok(
                        putLife
                            { life with
                                Dedicated =
                                    Some
                                        { DedicatedReviewerId = payload.DedicatedReviewerId
                                          ReviewerSessionId = payload.NewSessionId } }
                            indexedState
                    )

    let foldLegacySeed
        (payload: LegacyTodoSeedAdopted)
        (state: MagicTodoProjectionState)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state

        match life.LegacySeed with
        | Some(seedRef, seedDigest) when seedRef = payload.SeedTodoRef && seedDigest = payload.SeedTodoDigest ->
            Ok state
        | Some _ -> Error(MagicTodoFoldRejection.IdentityCorruption "LegacyTodoSeed")
        | None when not (Map.isEmpty life.Checkpoints) -> Error MagicTodoFoldRejection.LegacySeedAfterCheckpoint
        | None ->
            Ok(
                putLife
                    { life with
                        CurrentObligationsRef = Some(payload.SeedTodoRef, payload.SeedTodoDigest)
                        LegacySeed = Some(payload.SeedTodoRef, payload.SeedTodoDigest) }
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
