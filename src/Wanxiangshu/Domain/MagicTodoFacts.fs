namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Domain.MagicTodo

/// Magic Todo durable fact algebra (protocol §16).
///
/// Speculative / unwired: parallel family ready to plug into `Fact.AgentFact` /
/// Fold. Not yet dispatched from Journal.Boot. Illegal intermediate Stages are
/// deliberately absent — pending review is derived from Accepted ∧ ¬Concluded.
module MagicTodoFacts =

    /// Physical success evidence for TodoWriteAccepted (protocol §15 / §16.2).
    [<RequireQualifiedAccess>]
    type PhysicalSuccessEvidence =
        /// Live path: builtin executor returned and tool.execute.after ran.
        | LiveAfterSuccess
        /// Recovery path: full SDK snapshot shows completed ToolPart.
        | RecoveredCompletedToolPart

    /// Prepared: Magic validation passed; Base/Proposed/ReviewFrontier frozen.
    /// Not yet a checkpoint; does not derive review obligation.
    type TodoWritePrepared =
        {
            ManagerSessionId: SessionId
            ManagerLifeId: ManagerLifeId
            TodoWriteId: TodoWriteId
            ToolCallId: ToolCallId
            ToolPartOrdinal: int
            BaseTodoRef: BlobRef
            BaseTodoDigest: BlobDigest
            ProposedTodoRef: BlobRef
            ProposedTodoDigest: BlobDigest
            /// Exclusive frontier immediately before this tool-call in the Life.
            ReviewFrontier: XTraceCursor
            SemanticVersion: string
        }

    /// Accepted: checkpoint SSOT + process-review obligation SSOT.
    type TodoWriteAccepted =
        {
            ManagerLifeId: ManagerLifeId
            TodoWriteId: TodoWriteId
            ToolCallId: ToolCallId
            /// Journal line / envelope identity of the matching Prepared (opaque ref).
            PreparedFactRef: string
            InputDigest: string
            OutputDigest: string
            PhysicalSuccessEvidence: PhysicalSuccessEvidence
            SemanticVersion: string
        }

    /// Assignment freezes reviewer request-range start (not session Opening).
    type TodoProcessReviewAssigned =
        {
            ManagerLifeId: ManagerLifeId
            TodoWriteId: TodoWriteId
            TodoReviewId: TodoReviewId
            DedicatedReviewerId: DedicatedReviewerId
            ReviewerSessionId: SessionId
            /// Exclusive end after assignment authority landed in XTrace.
            /// Does NOT include the assignment prompt itself.
            ReviewWorkStartCursor: XTraceCursor
            ManagerReviewFrontier: XTraceCursor
        }

    /// ConsumableReview ≡ TodoReviewConcluded.
    /// Append ONLY when VerdictKnown ∧ ProcessReviewLWR record-ready in same snapshot.
    type TodoReviewConcluded =
        { ManagerLifeId: ManagerLifeId
          TodoWriteId: TodoWriteId
          TodoReviewId: TodoReviewId
          DedicatedReviewerId: DedicatedReviewerId
          ReviewerSessionId: SessionId
          Verdict: ProcessReviewVerdict
          WorkRecordRef: BlobRef
          WorkRecordDigest: BlobDigest
          ReviewerRecordFrontier: XTraceCursor
          ProviderRunId: ProviderRunIdentity
          ToolCallId: ToolCallId }

    type DedicatedTodoReviewerEnlisted =
        { ManagerLifeId: ManagerLifeId
          DedicatedReviewerId: DedicatedReviewerId
          ReviewerSessionId: SessionId }

    /// Only when Host proves the old physical session is permanently unrecoverable.
    type DedicatedTodoReviewerReplaced =
        { ManagerLifeId: ManagerLifeId
          DedicatedReviewerId: DedicatedReviewerId
          OldSessionId: SessionId
          NewSessionId: SessionId
          EvidenceRef: BlobRef }

    /// Upgrade-path only: seed legacy open Life before first Magic provider request.
    /// Forbidden for subsequent Lives in the same Host session.
    type LegacyTodoSeedAdopted =
        {
            ManagerSessionId: SessionId
            ManagerLifeId: ManagerLifeId
            SeedTodoRef: BlobRef
            SeedTodoDigest: BlobDigest
            /// Host-assigned Magic ids for each legacy row (position → TodoItemId).
            SeedItemIds: TodoItemId list
        }

    /// EvidenceKind for PrefixRebaseCommitted generalization (protocol §16.7).
    /// TodoCheckpoint rebase must enter existing ActivePrefixEpoch SSOT — not a
    /// parallel truth source. Probe retains today's ProbeId path.
    [<RequireQualifiedAccess>]
    type PrefixEvidenceKind =
        | Probe of probeId: string
        | TodoCheckpoint of triggerTodoWriteId: TodoWriteId * coveredBeforeTodoWriteId: TodoWriteId option

    /// Speculative PrefixRebaseCommitted payload with EvidenceKind.
    /// When wired, replaces / extends ContextFactCases.PrefixRebaseCommitted.
    type PrefixRebaseCommittedV2 =
        {
            SessionId: SessionId
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
            /// Y bundle proving PrefixCoverage complete-turn prefix (no LWR RawGap).
            YBundleRef: BlobRef option
            YBundleDigest: BlobDigest option
            ProviderPrefixDigest: string option
            /// Probe path only: solving run that promoted the candidate.
            SolvingProviderRun: ProviderRunIdentity option
        }

    /// One Magic Todo journal line. Parallel to Fact.AgentFact until wired.
    [<RequireQualifiedAccess>]
    type MagicTodoFact =
        | TodoWritePrepared of TodoWritePrepared
        | TodoWriteAccepted of TodoWriteAccepted
        | TodoProcessReviewAssigned of TodoProcessReviewAssigned
        | TodoReviewConcluded of TodoReviewConcluded
        | DedicatedTodoReviewerEnlisted of DedicatedTodoReviewerEnlisted
        | DedicatedTodoReviewerReplaced of DedicatedTodoReviewerReplaced
        | LegacyTodoSeedAdopted of LegacyTodoSeedAdopted
        | PrefixRebaseCommittedV2 of PrefixRebaseCommittedV2

    /// Constructor surface mirroring Fact.* modules.
    module Fact =
        let inline TodoWritePrepared (payload: TodoWritePrepared) = MagicTodoFact.TodoWritePrepared payload

        let inline TodoWriteAccepted (payload: TodoWriteAccepted) = MagicTodoFact.TodoWriteAccepted payload

        let inline TodoProcessReviewAssigned (payload: TodoProcessReviewAssigned) =
            MagicTodoFact.TodoProcessReviewAssigned payload

        let inline TodoReviewConcluded (payload: TodoReviewConcluded) =
            MagicTodoFact.TodoReviewConcluded payload

        let inline DedicatedTodoReviewerEnlisted (payload: DedicatedTodoReviewerEnlisted) =
            MagicTodoFact.DedicatedTodoReviewerEnlisted payload

        let inline DedicatedTodoReviewerReplaced (payload: DedicatedTodoReviewerReplaced) =
            MagicTodoFact.DedicatedTodoReviewerReplaced payload

        let inline LegacyTodoSeedAdopted (payload: LegacyTodoSeedAdopted) =
            MagicTodoFact.LegacyTodoSeedAdopted payload

        let inline PrefixRebaseCommittedV2 (payload: PrefixRebaseCommittedV2) =
            MagicTodoFact.PrefixRebaseCommittedV2 payload
