namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts

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

    type IncumbencyMagicTodoState =
        { IncumbencyId: IncumbencyId
          CurrentObligationsRef: (BlobRef * BlobDigest) option
          FirstAcceptedCheckpoint: TodoWriteId option
          LatestAcceptedCheckpoint: TodoWriteId option
          FirstPlanCommitment: TodoWriteId option
          LatestCommittedCheckpoint: TodoWriteId option
          PreviousCommittedCheckpoint: TodoWriteId option
          Checkpoints: Map<string, CheckpointRecord>
          LegacySeed: (BlobRef * BlobDigest) option }

    type MagicTodoProjectionState =
        { ByIncumbency: Map<string, IncumbencyMagicTodoState> }

    [<RequireQualifiedAccess>]
    type MagicTodoFoldRejection =
        | IncumbencyMismatch of expected: string * actual: string
        | PreparedMissingForAccept of todoWriteId: string
        | IdentityCorruption of field: string
        | LegacySeedAfterCheckpoint

    val empty: MagicTodoProjectionState
    val emptyIncumbency: incumbencyId: IncumbencyId -> IncumbencyMagicTodoState

    val tryIncumbency:
        incumbencyId: IncumbencyId -> state: MagicTodoProjectionState -> IncumbencyMagicTodoState option

    val ensureIncumbency:
        incumbencyId: IncumbencyId ->
        state: MagicTodoProjectionState ->
            IncumbencyMagicTodoState * MagicTodoProjectionState

    val isPlanCommitted: incumbency: IncumbencyMagicTodoState -> bool
    val acceptedEvidence: checkpoint: CheckpointRecord -> AcceptedCheckpointEvidence option
    val isAccepted: checkpoint: CheckpointRecord -> bool

    val foldPrepared:
        preparedFactRef: EventId ->
        payload: TodoWritePrepared ->
        state: MagicTodoProjectionState ->
            Result<MagicTodoProjectionState, MagicTodoFoldRejection>

    val foldAccepted:
        payload: TodoWriteAccepted ->
        state: MagicTodoProjectionState ->
            Result<MagicTodoProjectionState, MagicTodoFoldRejection>

    val foldLegacySeed:
        payload: LegacyTodoSeedAdopted ->
        state: MagicTodoProjectionState ->
            Result<MagicTodoProjectionState, MagicTodoFoldRejection>

    val fold:
        envelopeEventId: EventId ->
        state: MagicTodoProjectionState ->
        fact: MagicTodoFact ->
            Result<MagicTodoProjectionState, MagicTodoFoldRejection>
