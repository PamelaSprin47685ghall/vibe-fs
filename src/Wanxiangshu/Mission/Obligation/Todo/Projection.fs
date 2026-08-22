namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Composition.Durable

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
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// MagicTodoProjection — canonical todo list from facts.
///
/// Protocol SSOT #3. Accepted immediately owns CurrentObligations.
module MagicTodoProjection =

    type AcceptedCheckpointEvidence =
        { InputDigest: string
          OutputDigest: string }

    [<RequireQualifiedAccess>]
    type CheckpointLifecycle =
        | Prepared
        | Accepted of AcceptedCheckpointEvidence

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
          Lifecycle: CheckpointLifecycle }

    /// Per-Life Magic Todo derived view.
    /// DSL-state-combination: domain — checkpoint locators are durable event-integral
    /// evidence; each records facts already observed and none selects the next workflow step.
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
            FirstPlanCommitment: TodoWriteId option
            LatestCommittedCheckpoint: TodoWriteId option
            PreviousCommittedCheckpoint: TodoWriteId option
            Checkpoints: Map<string, CheckpointRecord>
            /// Upgrade-only canonical obligation seed locator.
            LegacySeed: (BlobRef * BlobDigest) option
        }

    type MagicTodoProjectionState =
        { ByLife: Map<string, LifeMagicTodoState> }

    [<RequireQualifiedAccess>]
    type MagicTodoFoldRejection =
        | LifeMismatch of expected: string * actual: string
        | PreparedMissingForAccept of todoWriteId: string
        | IdentityCorruption of field: string
        | LegacySeedAfterCheckpoint

    let empty: MagicTodoProjectionState = { ByLife = Map.empty }

    let private lifeKey (lifeId: ManagerLifeId) = ManagerLifeId.value lifeId

    let private writeKey (writeId: TodoWriteId) = TodoWriteId.value writeId

    let emptyLife (lifeId: ManagerLifeId) : LifeMagicTodoState =
        { LifeId = lifeId
          CurrentObligationsRef = None
          FirstAcceptedCheckpoint = None
          LatestAcceptedCheckpoint = None
          FirstPlanCommitment = None
          LatestCommittedCheckpoint = None
          PreviousCommittedCheckpoint = None
          Checkpoints = Map.empty
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

            life,
            { state with
                ByLife = Map.add (lifeKey lifeId) life state.ByLife }

    let private putLife (life: LifeMagicTodoState) (state: MagicTodoProjectionState) =
        { state with
            ByLife = Map.add (lifeKey life.LifeId) life state.ByLife }

    let isPlanCommitted (life: LifeMagicTodoState) : bool = life.FirstPlanCommitment.IsSome

    let acceptedEvidence (checkpoint: CheckpointRecord) : AcceptedCheckpointEvidence option =
        match checkpoint.Lifecycle with
        | CheckpointLifecycle.Prepared -> None
        | CheckpointLifecycle.Accepted evidence -> Some evidence

    let isAccepted (checkpoint: CheckpointRecord) : bool =
        acceptedEvidence checkpoint |> Option.isSome

    let private requirePreparedReplayIdentity
        (existing: CheckpointRecord)
        (payload: TodoWritePrepared)
        : Result<unit, MagicTodoFoldRejection> =
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
            Ok()

    let private insertPreparedCheckpoint
        (preparedFactRef: EventId)
        (payload: TodoWritePrepared)
        (life: LifeMagicTodoState)
        (state: MagicTodoProjectionState)
        =
        let key = writeKey payload.TodoWriteId

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
              Lifecycle = CheckpointLifecycle.Prepared }

        putLife
            { life with
                Checkpoints = Map.add key cp life.Checkpoints }
            state

    let private requireAcceptedReplay
        (cp: CheckpointRecord)
        (payload: TodoWriteAccepted)
        (state: MagicTodoProjectionState)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        match acceptedEvidence cp with
        | Some evidence when
            evidence.InputDigest = payload.InputDigest
            && evidence.OutputDigest = payload.OutputDigest
            ->
            Ok state
        | Some _ -> Error(MagicTodoFoldRejection.IdentityCorruption "AcceptedDigest")
        | None -> Error(MagicTodoFoldRejection.IdentityCorruption "AcceptedState")

    let private commitmentAfterAccept (life: LifeMagicTodoState) (writeId: TodoWriteId) (planCompleteDeclared: bool) =
        match life.FirstPlanCommitment with
        | None when planCompleteDeclared -> Some writeId, None, Some writeId
        | None -> None, None, None
        | Some first -> Some first, life.LatestCommittedCheckpoint, Some writeId

    let private acceptCheckpoint
        (payload: TodoWriteAccepted)
        (cp: CheckpointRecord)
        (life: LifeMagicTodoState)
        (state: MagicTodoProjectionState)
        =
        let key = writeKey payload.TodoWriteId

        let accepted =
            { InputDigest = payload.InputDigest
              OutputDigest = payload.OutputDigest }

        let cp =
            { cp with
                Lifecycle = CheckpointLifecycle.Accepted accepted }

        let firstAccepted =
            life.FirstAcceptedCheckpoint |> Option.orElse (Some payload.TodoWriteId)

        let firstCommitment, previousCommitted, latestCommitted =
            commitmentAfterAccept life payload.TodoWriteId cp.PlanCompleteDeclared

        putLife
            { life with
                Checkpoints = Map.add key cp life.Checkpoints
                FirstAcceptedCheckpoint = firstAccepted
                LatestAcceptedCheckpoint = Some payload.TodoWriteId
                FirstPlanCommitment = firstCommitment
                PreviousCommittedCheckpoint = previousCommitted
                LatestCommittedCheckpoint = latestCommitted
                CurrentObligationsRef = Some(cp.ProposedTodoRef, cp.ProposedTodoDigest) }
            state

    let foldPrepared
        (preparedFactRef: EventId)
        (payload: TodoWritePrepared)
        (state: MagicTodoProjectionState)
        : Result<MagicTodoProjectionState, MagicTodoFoldRejection> =
        let life, state = ensureLife payload.ManagerLifeId state
        let key = writeKey payload.TodoWriteId

        match Map.tryFind key life.Checkpoints with
        | Some existing ->
            result {
                do! requirePreparedReplayIdentity existing payload
                return state
            }
        | None -> Ok(insertPreparedCheckpoint preparedFactRef payload life state)

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
        | Some({ Lifecycle = CheckpointLifecycle.Prepared } as cp) -> Ok(acceptCheckpoint payload cp life state)
        | Some cp -> requireAcceptedReplay cp payload state

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
        | MagicTodoFact.LegacyTodoSeedAdopted payload -> foldLegacySeed payload state
        | MagicTodoFact.PrefixRebaseCommittedV2 _ -> Ok state
