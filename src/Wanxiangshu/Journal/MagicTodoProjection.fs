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
        { TodoWriteId: TodoWriteId
          ToolCallId: ToolCallId
          ToolPartOrdinal: int
          BaseTodoDigest: BlobDigest
          ProposedTodoDigest: BlobDigest
          BaseTodoRef: BlobRef
          ProposedTodoRef: BlobRef
          ReviewFrontier: XTraceCursor
          PreparedFactRef: string option
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
        { LifeId: ManagerLifeId
          /// Settled current (Ck for the next prepare). Empty for a normal new Life.
          SettledCurrent: MagicTodoList
          /// Optimistic working proposal after latest Accepted, if any (Pk).
          /// Canonical settlement still waits for Concluded of that checkpoint.
          WorkingProposal: MagicTodoList option
          /// Accepted chain in order (lag-1 desired cutoff source).
          AcceptedOrder: TodoWriteId list
          Checkpoints: Map<string, CheckpointRecord>
          Dedicated: DedicatedReviewerState option
          /// Blob list bodies live in the blob store; projection keeps digests/refs.
          LegacySeedAdopted: bool }

    type MagicTodoProjectionState =
        { ByLife: Map<string, LifeMagicTodoState> }

    [<RequireQualifiedAccess>]
    type MagicTodoFoldRejection =
        | LifeMismatch of expected: string * actual: string
        | PreparedMissingForAccept of todoWriteId: string
        | IdentityCorruption of field: string
        | DuplicateConcluded of todoWriteId: string
        | AssignmentWithoutAccepted of todoWriteId: string
        | ConcludedWithoutAccepted of todoWriteId: string
        | LegacySeedAfterCheckpoint
        | DedicatedMissingForReplace

    let empty: MagicTodoProjectionState = { ByLife = Map.empty }

    let private lifeKey (lifeId: ManagerLifeId) = ManagerLifeId.value lifeId

    let private writeKey (writeId: TodoWriteId) = TodoWriteId.value writeId

    let emptyLife (lifeId: ManagerLifeId) : LifeMagicTodoState =
        { LifeId = lifeId
          SettledCurrent = []
          WorkingProposal = None
          AcceptedOrder = []
          Checkpoints = Map.empty
          Dedicated = None
          LegacySeedAdopted = false }

    let tryLife (lifeId: ManagerLifeId) (state: MagicTodoProjectionState) : LifeMagicTodoState option =
        Map.tryFind (lifeKey lifeId) state.ByLife

    let ensureLife (lifeId: ManagerLifeId) (state: MagicTodoProjectionState) : LifeMagicTodoState * MagicTodoProjectionState =
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
        | Some cp ->
            Error(MagicTodoReject.AwaitingConsumableReview(TodoWriteId.value cp.TodoWriteId))

    /// Desired lag-1 cutoff from Accepted chain (no Requested fact).
    let desiredLag1 (life: LifeMagicTodoState) : TodoWriteId option =
        MagicTodo.desiredLag1Cutoff life.AcceptedOrder

    /// Apply settlement when consuming a Concluded review: updates SettledCurrent.
    let private applySettlement
        (decodeList: BlobRef -> BlobDigest -> MagicTodoList)
        (life: LifeMagicTodoState)
        (cp: CheckpointRecord)
        (concluded: TodoReviewConcluded)
        : LifeMagicTodoState =
        let baseList = decodeList cp.BaseTodoRef cp.BaseTodoDigest
        let proposed = decodeList cp.ProposedTodoRef cp.ProposedTodoDigest
        let settled = MagicTodo.settle baseList proposed concluded.Verdict

        { life with
            SettledCurrent = settled
            // Working proposal cleared once settled; next Accepted sets a new one.
            WorkingProposal = None }

    let foldPrepared (payload: TodoWritePrepared) (state: MagicTodoProjectionState) : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state
        let key = writeKey payload.TodoWriteId

        match Map.tryFind key life.Checkpoints with
        | Some existing when existing.Accepted || existing.Concluded.IsSome ->
            // Identity corruption if Prepared fields disagree with frozen record.
            if existing.BaseTodoDigest <> payload.BaseTodoDigest then
                Error(MagicTodoFoldRejection.IdentityCorruption "BaseTodoDigest")
            elif existing.ProposedTodoDigest <> payload.ProposedTodoDigest then
                Error(MagicTodoFoldRejection.IdentityCorruption "ProposedTodoDigest")
            elif existing.ToolPartOrdinal <> payload.ToolPartOrdinal then
                Error(MagicTodoFoldRejection.IdentityCorruption "ToolPartOrdinal")
            else
                Ok state
        | Some _ ->
            // Idempotent replay of same Prepared.
            Ok state
        | None ->
            let cp =
                { TodoWriteId = payload.TodoWriteId
                  ToolCallId = payload.ToolCallId
                  ToolPartOrdinal = payload.ToolPartOrdinal
                  BaseTodoDigest = payload.BaseTodoDigest
                  ProposedTodoDigest = payload.ProposedTodoDigest
                  BaseTodoRef = payload.BaseTodoRef
                  ProposedTodoRef = payload.ProposedTodoRef
                  ReviewFrontier = payload.ReviewFrontier
                  PreparedFactRef = None
                  InputDigest = None
                  OutputDigest = None
                  Accepted = false
                  Assignment = None
                  Concluded = None }

            Ok(putLife { life with Checkpoints = Map.add key cp life.Checkpoints } state)

    let foldAccepted (payload: TodoWriteAccepted) (state: MagicTodoProjectionState) : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state
        let key = writeKey payload.TodoWriteId

        match Map.tryFind key life.Checkpoints with
        | None -> Error(MagicTodoFoldRejection.PreparedMissingForAccept key)
        | Some cp when cp.Accepted ->
            // Idempotent; live/recovery digests must converge.
            match cp.InputDigest, cp.OutputDigest with
            | Some i, Some o when i = payload.InputDigest && o = payload.OutputDigest -> Ok state
            | Some _, Some _ -> Error(MagicTodoFoldRejection.IdentityCorruption "AcceptedDigest")
            | _ ->
                let cp =
                    { cp with
                        PreparedFactRef = Some payload.PreparedFactRef
                        InputDigest = Some payload.InputDigest
                        OutputDigest = Some payload.OutputDigest }

                Ok(putLife { life with Checkpoints = Map.add key cp life.Checkpoints } state)
        | Some cp ->
            let cp =
                { cp with
                    Accepted = true
                    PreparedFactRef = Some payload.PreparedFactRef
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

    /// Overlay WorkingProposal after Accepted (blob decode stays at the call site).
    let withWorkingProposal (lifeId: ManagerLifeId) (proposed: MagicTodoList) (state: MagicTodoProjectionState) =
        match tryLife lifeId state with
        | None -> state
        | Some life -> putLife { life with WorkingProposal = Some proposed } state

    let foldAssigned (payload: TodoProcessReviewAssigned) (state: MagicTodoProjectionState) : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state
        let key = writeKey payload.TodoWriteId

        match Map.tryFind key life.Checkpoints with
        | None
        | Some { Accepted = false } -> Error(MagicTodoFoldRejection.AssignmentWithoutAccepted key)
        | Some cp ->
            match cp.Assignment with
            | Some existing when existing.TodoReviewId = payload.TodoReviewId -> Ok state
            | Some _ -> Error(MagicTodoFoldRejection.IdentityCorruption "TodoReviewId")
            | None ->
                Ok(
                    putLife
                        { life with
                            Checkpoints = Map.add key { cp with Assignment = Some payload } life.Checkpoints }
                        state
                )

    let foldConcluded
        (decodeList: BlobRef -> BlobDigest -> MagicTodoList)
        (payload: TodoReviewConcluded)
        (state: MagicTodoProjectionState)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state
        let key = writeKey payload.TodoWriteId

        match Map.tryFind key life.Checkpoints with
        | None
        | Some { Accepted = false } -> Error(MagicTodoFoldRejection.ConcludedWithoutAccepted key)
        | Some { Concluded = Some _ } -> Error(MagicTodoFoldRejection.DuplicateConcluded key)
        | Some cp ->
            let cp = { cp with Concluded = Some payload }
            let life = { life with Checkpoints = Map.add key cp life.Checkpoints }
            // Settlement applies when Concluded becomes durable — next prepare
            // reads SettledCurrent as C(k+1). Suicide drain uses the same path.
            let life = applySettlement decodeList life cp payload
            Ok(putLife life state)

    let foldDedicatedEnlisted (payload: DedicatedTodoReviewerEnlisted) (state: MagicTodoProjectionState) =
        let life, state = ensureLife payload.ManagerLifeId state

        match life.Dedicated with
        | Some d when d.DedicatedReviewerId = payload.DedicatedReviewerId && d.ReviewerSessionId = payload.ReviewerSessionId ->
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
        (decodeList: BlobRef -> BlobDigest -> MagicTodoList)
        (payload: LegacyTodoSeedAdopted)
        (state: MagicTodoProjectionState)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state

        if life.LegacySeedAdopted then
            Ok state
        elif not (List.isEmpty life.AcceptedOrder) then
            Error MagicTodoFoldRejection.LegacySeedAfterCheckpoint
        else
            let seeded = decodeList payload.SeedTodoRef payload.SeedTodoDigest

            Ok(
                putLife
                    { life with
                        SettledCurrent = seeded
                        LegacySeedAdopted = true }
                    state
            )

    /// Dispatch one Magic Todo fact. PrefixRebaseCommittedV2 is owned by
    /// PrefixEpochProjection once wired — ignored here (no todo-list effect).
    let fold
        (decodeList: BlobRef -> BlobDigest -> MagicTodoList)
        (state: MagicTodoProjectionState)
        (fact: MagicTodoFact)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        match fact with
        | MagicTodoFact.TodoWritePrepared payload -> foldPrepared payload state
        | MagicTodoFact.TodoWriteAccepted payload -> foldAccepted payload state
        | MagicTodoFact.TodoProcessReviewAssigned payload -> foldAssigned payload state
        | MagicTodoFact.TodoReviewConcluded payload -> foldConcluded decodeList payload state
        | MagicTodoFact.DedicatedTodoReviewerEnlisted payload -> foldDedicatedEnlisted payload state
        | MagicTodoFact.DedicatedTodoReviewerReplaced payload -> foldDedicatedReplaced payload state
        | MagicTodoFact.LegacyTodoSeedAdopted payload -> foldLegacySeed decodeList payload state
        | MagicTodoFact.PrefixRebaseCommittedV2 _ -> Ok state
