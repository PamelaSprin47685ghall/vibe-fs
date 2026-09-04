namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Context.Trace

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Relay

/// Magic Todo durable fact algebra (TODO-004/006/012).
///
/// Canonical codec bytes enter the top-level journal `Fact.MagicTodo` boundary;
/// Boot decodes and folds them into the one MagicTodo projection.
module MagicTodoFacts =

    /// Physical success evidence for TodoWriteAccepted (protocol §15 / §16.2).
    [<RequireQualifiedAccess>]
    type PhysicalSuccessEvidence =
        /// Live path: builtin executor returned and tool.execute.after ran.
        | LiveAfterSuccess
        /// Recovery path: full SDK snapshot shows completed ToolPart.
        | RecoveredCompletedToolPart

    /// Prepared: Magic validation passed; Base/Proposed/ReviewFrontier frozen.
    /// Not yet a checkpoint.
    type TodoWritePrepared =
        {
            ManagerSessionId: SessionId
            IncumbencyId: IncumbencyId
            TodoWriteId: TodoWriteId
            ToolCallId: ToolCallId
            ToolPartOrdinal: int
            BaseTodoRef: BlobRef
            BaseTodoDigest: BlobDigest
            ProposedTodoRef: BlobRef
            ProposedTodoDigest: BlobDigest
            /// Raw provider commitment declaration frozen before physical execution.
            /// It is an observed business fact, not a workflow stage.
            PlanCompleteDeclared: bool
            /// Digest of canonical `{planComplete,workingOn,obligations:[{name,horizon,work}]}` provider arguments.
            ProviderInputDigest: string
            /// Exclusive frontier immediately before this tool-call in the Life.
            ReviewFrontier: XTraceCursor
            SemanticVersion: string
        }

    /// Accepted: checkpoint SSOT.
    type TodoWriteAccepted =
        {
            IncumbencyId: IncumbencyId
            TodoWriteId: TodoWriteId
            ToolCallId: ToolCallId
            /// Journal envelope identity of the matching Prepared.
            PreparedFactRef: EventId
            InputDigest: string
            OutputDigest: string
            PhysicalSuccessEvidence: PhysicalSuccessEvidence
            SemanticVersion: string
        }

    /// Upgrade-path only: seed one already-open Life with a canonical obligation
    /// account before its first Magic provider request. Historical facts may carry
    /// an extra SeedItemIds field; v2 ignores it on decode.
    type LegacyTodoSeedAdopted =
        { ManagerSessionId: SessionId
          IncumbencyId: IncumbencyId
          SeedTodoRef: BlobRef
          SeedTodoDigest: BlobDigest }

    /// EvidenceKind for PrefixRebaseCommitted generalization (protocol §16.7).
    /// TodoCheckpoint rebase must enter existing ActivePrefixEpoch SSOT — not a
    /// parallel truth source. Probe retains today's ProbeId path.
    [<RequireQualifiedAccess>]
    type PrefixEvidenceKind =
        | Probe of probeId: string
        | TodoCheckpoint of triggerTodoWriteId: TodoWriteId * coveredBeforeTodoWriteId: TodoWriteId option

    /// Todo-aware PrefixRebaseCommitted payload with EvidenceKind.
    /// Fold routes it into the existing PrefixEpochProjection SSOT.
    /// DSL-state-combination: domain — optional life/evidence/provider facets
    /// describe one durable prefix rebase fact; they are proof metadata, not a
    /// stored continuation stage.
    type PrefixRebaseCommittedV2 =
        {
            SessionId: SessionId
            IncumbencyId: IncumbencyId option
            PreviousEpochId: PrefixEpochId
            NextEpochId: PrefixEpochId
            EvidenceKind: PrefixEvidenceKind
            FrozenRecordPrefixRef: BlobRef
            FrozenRecordPrefixDigest: BlobDigest
            CutoffExclusive: int
            CoveredPrefixDigest: string
            SealRoot: string
            SyntheticMessageId: string
            /// Y bundle proving PrefixCoverage complete-turn prefix (no LWR RawGap).
            YBundleRef: BlobRef option
            YBundleDigest: BlobDigest option
            ProviderPrefixDigest: string option
            /// Probe path only: solving run that promoted the candidate.
            SolvingProviderRun: ProviderRunIdentity option
        }

    [<RequireQualifiedAccess>]
    type MagicTodoFact =
        | TodoWritePrepared of TodoWritePrepared
        | TodoWriteAccepted of TodoWriteAccepted
        | LegacyTodoSeedAdopted of LegacyTodoSeedAdopted
        | PrefixRebaseCommittedV2 of PrefixRebaseCommittedV2

    /// Constructor surface mirroring Fact.* modules.
    module Fact =
        let inline TodoWritePrepared (payload: TodoWritePrepared) = MagicTodoFact.TodoWritePrepared payload

        let inline TodoWriteAccepted (payload: TodoWriteAccepted) = MagicTodoFact.TodoWriteAccepted payload

        let inline LegacyTodoSeedAdopted (payload: LegacyTodoSeedAdopted) =
            MagicTodoFact.LegacyTodoSeedAdopted payload

        let inline PrefixRebaseCommittedV2 (payload: PrefixRebaseCommittedV2) =
            MagicTodoFact.PrefixRebaseCommittedV2 payload
