namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo

module MagicTodoFacts =
    [<RequireQualifiedAccess>]
    type PhysicalSuccessEvidence =
        | LiveAfterSuccess
        | RecoveredCompletedToolPart

    type TodoWritePrepared =
        { ManagerSessionId: SessionId
          ManagerLifeId: ManagerLifeId
          TodoWriteId: TodoWriteId
          ToolCallId: ToolCallId
          ToolPartOrdinal: int
          BaseTodoRef: BlobRef
          BaseTodoDigest: BlobDigest
          ProposedTodoRef: BlobRef
          ProposedTodoDigest: BlobDigest
          PlanCompleteDeclared: bool
          ProviderInputDigest: string
          ReviewFrontier: XTraceCursor
          SemanticVersion: string }

    type TodoWriteAccepted =
        { ManagerLifeId: ManagerLifeId
          TodoWriteId: TodoWriteId
          ToolCallId: ToolCallId
          PreparedFactRef: EventId
          InputDigest: string
          OutputDigest: string
          PhysicalSuccessEvidence: PhysicalSuccessEvidence
          SemanticVersion: string }

    type LegacyTodoSeedAdopted =
        { ManagerSessionId: SessionId
          ManagerLifeId: ManagerLifeId
          SeedTodoRef: BlobRef
          SeedTodoDigest: BlobDigest }

    [<RequireQualifiedAccess>]
    type PrefixEvidenceKind =
        | Probe of probeId: string
        | TodoCheckpoint of triggerTodoWriteId: TodoWriteId * coveredBeforeTodoWriteId: TodoWriteId option

    type PrefixRebaseCommittedV2 =
        { SessionId: SessionId
          ManagerLifeId: ManagerLifeId option
          PreviousEpochId: PrefixEpochId
          NextEpochId: PrefixEpochId
          EvidenceKind: PrefixEvidenceKind
          FrozenRecordPrefixRef: BlobRef
          FrozenRecordPrefixDigest: BlobDigest
          CutoffExclusive: int
          CoveredPrefixDigest: string
          SealRoot: string
          SyntheticMessageId: string
          YBundleRef: BlobRef option
          YBundleDigest: BlobDigest option
          ProviderPrefixDigest: string option
          SolvingProviderRun: ProviderRunIdentity option }

    [<RequireQualifiedAccess>]
    type MagicTodoFact =
        | TodoWritePrepared of TodoWritePrepared
        | TodoWriteAccepted of TodoWriteAccepted
        | LegacyTodoSeedAdopted of LegacyTodoSeedAdopted
        | PrefixRebaseCommittedV2 of PrefixRebaseCommittedV2

    module Fact =
        val inline TodoWritePrepared: payload: TodoWritePrepared -> MagicTodoFact
        val inline TodoWriteAccepted: payload: TodoWriteAccepted -> MagicTodoFact
        val inline LegacyTodoSeedAdopted: payload: LegacyTodoSeedAdopted -> MagicTodoFact
        val inline PrefixRebaseCommittedV2: payload: PrefixRebaseCommittedV2 -> MagicTodoFact
