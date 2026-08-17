namespace Wanxiangshu.Mission.Review.Judgement

open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
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

    let private append (sessionId: SessionId) (providerRun: ProviderRunIdentity) fact journal =
        task {
            match! AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) fact journal with
            | Ok updated -> return Ok updated
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    let recordJudgement (journal: AgentJournal) (submission: VerdictSubmission) : Task<Result<unit, string>> =
        taskResult {
            let fact =
                ReviewFact.ReviewVerdictRecorded
                    {| ReviewerSessionId = submission.ReviewerSessionId
                       ManagerSessionId = submission.ManagerSessionId
                       BarrierId = submission.BarrierId
                       GitTreeHash = submission.GitTreeHash
                       ProviderRun = submission.ProviderRun
                       ToolCallId = submission.ToolCallId
                       Verdict = submission.Verdict |}

            let! _ = append submission.ReviewerSessionId submission.ProviderRun fact journal
            return ()
        }

    let private verdictWitness (tree: GitTreeHash) (judgement: ReviewJudgement) : VerdictWitness =
        { ProviderRun = judgement.ProviderRun
          ToolCallId = judgement.ToolCallId
          GitTreeHash = tree
          ReviewerSessionId = judgement.ReviewerSessionId }

    let recordConfirmation
        (journal: AgentJournal)
        (managerJobId: ManagerJobId option)
        (worktreeIdentity: WorktreeIdentity option)
        (managerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (tree: GitTreeHash)
        (first: ReviewJudgement)
        (second: ReviewJudgement)
        : Task<Result<unit, string>> =
        taskResult {
            let firstWitness = verdictWitness tree first
            let secondWitness = verdictWitness tree second

            let! _ =
                ReviewWitness.confirm
                    barrierId
                    first.PhysicalUserMessageId
                    second.PhysicalUserMessageId
                    firstWitness
                    secondWitness
                |> Result.requireSome "second PERFECT is not causally distinct from the first"

            let fact =
                ReviewFact.ConfirmedReviewWitness
                    {| ManagerJobId = managerJobId
                       ManagerSessionId = managerSessionId
                       ReviewerSessionId = second.ReviewerSessionId
                       WorktreeIdentity = worktreeIdentity
                       BarrierId = barrierId
                       GitTreeHash = tree
                       FirstProviderRun = first.ProviderRun
                       FirstToolCallId = first.ToolCallId
                       FirstPhysicalUserMessageId = first.PhysicalUserMessageId
                       SecondProviderRun = second.ProviderRun
                       SecondPhysicalUserMessageId = second.PhysicalUserMessageId
                       SecondToolCallId = second.ToolCallId |}

            let! _ = append second.ReviewerSessionId second.ProviderRun fact journal
            return ()
        }
