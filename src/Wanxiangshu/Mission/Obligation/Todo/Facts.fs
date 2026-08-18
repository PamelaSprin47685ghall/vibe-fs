namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo

/// Magic Todo durable fact algebra (TODO-004/006/012).
///
/// Canonical codec bytes enter the top-level journal `Fact.MagicTodo` boundary;
/// Boot decodes and folds them into the one MagicTodo projection. Illegal
/// intermediate stages are absent: pending review is Accepted ∧ ¬Concluded.
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
            /// Raw provider commitment declaration frozen before physical execution.
            /// It is an observed business fact, not a workflow stage.
            PlanCompleteDeclared: bool
            /// Digest of canonical `{planComplete,workingOn,obligations:[{name,horizon,work}]}` provider arguments.
            ProviderInputDigest: string
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
            /// Journal envelope identity of the matching Prepared.
            PreparedFactRef: EventId
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
            /// Reviewer frontier frozen BEFORE the assignment dispatch (HOST-021).
            /// The LWR request range [ReviewWorkStartCursor, closure frontier)
            /// therefore includes the assignment prompt itself and everything
            /// the reviewer produces for this checkpoint.
            ReviewWorkStartCursor: XTraceCursor
            ManagerReviewFrontier: XTraceCursor
        }

    /// ConsumableReview ≡ TodoReviewConcluded.
    /// Append ONLY when VerdictKnown ∧ ProcessReviewLWR record-ready in same snapshot.
    type TodoReviewConcluded =
        {
            ManagerLifeId: ManagerLifeId
            TodoWriteId: TodoWriteId
            TodoReviewId: TodoReviewId
            DedicatedReviewerId: DedicatedReviewerId
            ReviewerSessionId: SessionId
            Verdict: ProcessReviewVerdict
            WorkRecordRef: BlobRef
            WorkRecordDigest: BlobDigest
            /// Legacy persisted-wire compatibility echo. New v2 writers copy the
            /// reviewed checkpoint's ProposedTodo locator here; projection ignores
            /// these fields as CurrentObligations writers (TODO-005).
            SettledTodoRef: BlobRef
            SettledTodoDigest: BlobDigest
            ReviewerRecordFrontier: XTraceCursor
            ProviderRunId: ProviderRunIdentity
            ToolCallId: ToolCallId
        }

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

    /// Upgrade-path only: seed one already-open Life with a canonical obligation
    /// account before its first Magic provider request. Historical facts may carry
    /// an extra SeedItemIds field; v2 ignores it on decode.
    type LegacyTodoSeedAdopted =
        { ManagerSessionId: SessionId
          ManagerLifeId: ManagerLifeId
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
