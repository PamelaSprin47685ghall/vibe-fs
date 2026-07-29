namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Domain

type ManagerId = private ManagerId of string

module ManagerId =
    let create (value: string) = ManagerId value
    let value (ManagerId v) = v

type GitTreeHash = private GitTreeHash of string

module GitTreeHash =
    let create (value: string) = GitTreeHash value
    let value (GitTreeHash v) = v

type EffectId = private EffectId of string

module EffectId =
    let create (value: string) = EffectId value
    let value (EffectId v) = v

type CandidateId = private CandidateId of string

module CandidateId =
    let create (value: string) = CandidateId value
    let value (CandidateId v) = v

type ProjectionSnapshot = string
type BlogText = string

type ActivePrefixEpochProjection =
    { EpochId: string
      FrozenB: BlogText
      CutoffMessageIndex: int
      CoveredPrefixDigest: string }

type CompanionProjection =
    { LastSuccessfulProjection: ProjectionSnapshot option
      LatestB: BlogText option
      ActivePrefixEpoch: ActivePrefixEpochProjection option
      ReplacementActive: bool }

    member this.PrefixReplacementEnabled = this.ReplacementActive

type AgentLinkageProjection =
    { LinkedChildren: Map<ChildId, string>
      LinkedRoles: Map<ChildId, string> }

type ReviewGuardProjection =
    {
        LastGitTreeHash: GitTreeHash option
        ConsecutivePerfects: int
        IsConfirmed: bool
        /// Reviewer that supplied the currently confirmed double-PERFECT witness.
        ConfirmedReviewerSessionId: SessionId option
        /// Provider run that made the second PERFECT; its terminal idle closes the review.
        ConfirmedProviderRunId: string option
        AcceptedGuardKey: string option
        RecentToolCallIds: string list
        RecentProviderRunIds: string list
        /// Physical Host message id of the confirmation prompt. Second PERFECT is
        /// proven only when its root user message id equals this id.
        ConfirmationPhysicalMessageId: string option
        /// Authority root of the original reviewer task (informational / restart).
        AuthorityRootUserMessageId: string option
        CurrentBarrierKey: string option
    }

/// Verified HumanRoot prompt identity and the transcript session that owns its text.
type ReviewRequirementInput =
    { SourceSessionId: SessionId
      MessageId: MessageId }

/// Human prompts awaiting the next completed review. Prompt text stays in the
/// Host transcript and is fetched only when creating a reviewer.
type ReviewRequirementProjection =
    { HumanPromptInputs: ReviewRequirementInput list
      LastConfirmedIdleAssistantMessageId: MessageId option }

type FallbackProjection =
    {
        /// Logical Run that owns this Fallback cursor. New Authority Root resets.
        LogicalRunId: string
        AuthorityRootUserMessageId: string
        /// Modulo-4 cursor: 0→A, 1→A, 2→B, 3→B. Infinite cycle; never Dead.
        Offset: byte
        LastProviderAttempt: int64 option
        /// Bounded durable identities for restart-safe failure dedupe.
        RecentFailureIds: string list
    }

type CandidateStatus =
    | Registered of candidateId: CandidateId * branch: string * commitHash: string
    | Published of candidateId: CandidateId * commitHash: string
    | Rejected of candidateId: CandidateId * reason: string

type ManagerState = { Status: CandidateStatus option }

type ManagerJob =
    { WorktreePath: string
      Branch: string
      CandidateId: CandidateId option
      CandidateCommit: string option
      PublishedCommit: string option
      Prompt: string
      // Durable publish-chain barrier facts (latest wins; keyed by commit identity
      // so a stale barrier never matches a new HEAD on re-run):
      PreRebaseReviewCommit: string option
      RebasedCommit: string option
      ConflictFiles: string list option
      PostRebaseReviewCommit: string option
      PublishClaimHead: string option }

type OrchestratorProjection =
    { ManagerJobs: Map<ManagerId, ManagerJob>
      Managers: Map<ManagerId, ManagerState>
      PublishedCommit: string option }

type EffectStatus =
    | Requested of target: string * payload: string
    | Accepted of target: string * payload: string * result: string

type DurableEffectProjection =
    { Current: (EffectId * EffectStatus) option }

type SessionAgentProjection =
    { Companion: CompanionProjection option
      Linkage: AgentLinkageProjection option
      ReviewGuard: ReviewGuardProjection option
      ReviewRequirements: ReviewRequirementProjection option
      Fallback: FallbackProjection option
      PromptAuthority: PromptAuthority.PromptAuthorityProjection option
      Effects: DurableEffectProjection option }

type AgentProjectionSet =
    { Sessions: Map<SessionId, SessionAgentProjection>
      Orchestrator: OrchestratorProjection }
