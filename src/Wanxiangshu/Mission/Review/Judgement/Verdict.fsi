namespace Wanxiangshu.Mission.Review.Judgement

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Persistence.Journal

/// One `judge` call interpreted in the immutable review scope owned by its CE.
type VerdictSubmission =
    { BarrierId: ReviewBarrierId
      GitTreeHash: GitTreeHash
      ManagerSessionId: SessionId
      ReviewerSessionId: SessionId
      ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      Verdict: ReviewGuardVerdict }

/// Durable review facts are outputs of the direct CE, never its program counter.
module VerdictWorkflow =

    val recordJudgement: journal: AgentJournal -> submission: VerdictSubmission -> Task<Result<unit, string>>

    val recordConfirmation:
        journal: AgentJournal ->
        managerJobId: ManagerJobId option ->
        worktreeIdentity: WorktreeIdentity option ->
        managerSessionId: SessionId ->
        barrierId: ReviewBarrierId ->
        tree: GitTreeHash ->
        expectedSecondPhysicalUserMessageId: PhysicalUserMessageId ->
        first: ReviewJudgement ->
        second: ReviewJudgement ->
            Task<Result<unit, string>>
