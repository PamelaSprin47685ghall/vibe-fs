namespace Wanxiangshu.Next.Tests.Integration

open System
open System.Collections.Generic
open System.Threading.Tasks
open Xunit
open Fable.Core
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Orchestrator
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module OrchestratorRecoveryTests =
    open OrchestratorRecoveryFixtures

    [<Fact>]
    let ``post_candidate_recovery_skips_pre_review_and_candidate_append`` () =
        task {
            let managerId = "m-post-candidate"
            let worktree = "/wt/post-candidate"
            let candidate = "candidate-post-candidate"

            let facts =
                [ managerJob managerId worktree "prompt"
                  AgentFact.OrchestratorPreRebaseReviewConfirmed
                      {| ManagerId = managerId
                         CandidateId = candidate
                         CommitHash = "c1" |}
                  AgentFact.OrchestratorCandidateRegistered
                      {| ManagerId = managerId
                         CandidateId = candidate
                         Branch = "manager/m-post-candidate"
                         CommitHash = "c1" |} ]

            let rebaseCalls = ref 0

            let! verdict, recorded =
                _runRecovery facts (gitPort rebaseCalls "c1") (authority "c1" "target") managerId worktree "prompt"

            match verdict with
            | OrchestratorVerdict.Published _ -> ()
            | other -> Assert.Fail(sprintf "expected Published, got %A" other)

            Assert.Equal(1, rebaseCalls.Value)

            Assert.Equal(
                0,
                recorded
                |> Seq.filter (function
                    | AgentFact.OrchestratorPreRebaseReviewConfirmed _ -> true
                    | _ -> false)
                |> Seq.length
            )

            Assert.Equal(
                0,
                recorded
                |> Seq.filter (function
                    | AgentFact.OrchestratorCandidateRegistered _ -> true
                    | _ -> false)
                |> Seq.length
            )
        }

    [<Fact>]
    let ``post_rebase_recovery_skips_rebase_and_runs_post_review`` () =
        task {
            let managerId = "m-post-rebase"
            let worktree = "/wt/post-rebase"
            let candidate = "candidate-post-rebase"

            let facts =
                [ managerJob managerId worktree "prompt"
                  AgentFact.OrchestratorPreRebaseReviewConfirmed
                      {| ManagerId = managerId
                         CandidateId = candidate
                         CommitHash = "c1" |}
                  AgentFact.OrchestratorCandidateRegistered
                      {| ManagerId = managerId
                         CandidateId = candidate
                         Branch = "manager/m-post-rebase"
                         CommitHash = "c1" |}
                  AgentFact.OrchestratorRebased
                      {| ManagerId = managerId
                         CandidateId = candidate
                         RebasedCommit = "c1" |} ]

            let rebaseCalls = ref 0

            let! verdict, recorded =
                _runRecovery facts (gitPort rebaseCalls "c1") (authority "c1" "target") managerId worktree "prompt"

            match verdict with
            | OrchestratorVerdict.Published _ -> ()
            | other -> Assert.Fail(sprintf "expected Published, got %A" other)

            Assert.Equal(0, rebaseCalls.Value)

            Assert.Equal(
                1,
                recorded
                |> Seq.filter (function
                    | AgentFact.OrchestratorPostRebaseReviewConfirmed _ -> true
                    | _ -> false)
                |> Seq.length
            )
        }

    [<Fact>]
    let ``conflict_resume_prompt_keeps_same_manager_context`` () =
        let prompt =
            OrchestratorPrompts.buildConflictResumePrompt "original" [ "src/a.fs"; "src/b.fs" ]

        Assert.Contains("[CONFLICT RESUMPTION]", prompt)
        Assert.Contains("src/a.fs", prompt)
        Assert.Contains("src/b.fs", prompt)
        Assert.Contains("original", prompt)

    [<Fact>]
    let ``reconcile_writes_candidate_commit_not_target_head`` () =
        withTempDir (fun tempDir ->
            task {
                // Real repo: candidate commit is a proper ancestor of the current
                // target HEAD. Reconcile must prove containment via git and record
                // the CANDIDATE sha — not the target HEAD that merely contains it.
                let repoDir, candidate, targetHead = _GitTestRepo._createTwoCommitRepo tempDir
                Assert.False((candidate = targetHead), "candidate and target HEAD must differ")

                let runtimeId = RuntimeId.create "rt-reconcile"
                let managerId = "m-reconcile"

                use journal = AgentJournal.create tempDir runtimeId 1 DateTimeOffset.UtcNow

                // Seed: ManagerJobCreated + CandidateRegistered (candidate exists, not yet published)
                let jobFact =
                    AgentFact.OrchestratorManagerJobCreated
                        {| ManagerId = managerId
                           WorktreePath = "/wt/reconcile"
                           Branch = sprintf "manager/%s" managerId
                           Prompt = "p" |}

                Assert.True(Result.isOk (AgentJournal.appendAgent StreamId.Workspace None jobFact journal))

                let candFact =
                    AgentFact.OrchestratorCandidateRegistered
                        {| ManagerId = managerId
                           CandidateId = "cand-reconcile"
                           Branch = sprintf "manager/%s" managerId
                           CommitHash = candidate |}

                Assert.True(Result.isOk (AgentJournal.appendAgent StreamId.Workspace None candFact journal))

                // Real authority: GetTargetHead returns the actual main HEAD
                // (≠ candidate); the ancestor check runs real git merge-base.
                let! reconciled =
                    OrchestratorAuthority.reconcilePublishedFromAuthority
                        (Some journal)
                        (OrchestratorAuthority.createPort ())
                        repoDir
                        "main"

                Assert.Equal(1, reconciled.Length)
                let recordedMgr, recordedCommit = reconciled.[0]
                Assert.Equal(managerId, recordedMgr)
                Assert.Equal(candidate, recordedCommit)

                // The Published fact in the journal must record the candidate commit.
                let snapshot = AgentJournal.snapshot journal
                Assert.Equal(Some candidate, snapshot.AgentProjections.Orchestrator.PublishedCommit)

                // ff/Published crash window (ff-only landed, Published not yet
                // written, crash): reconcile writes exactly ONE Published fact
                // and the active ManagerJobs projection is drained to zero.
                Assert.Equal(0, snapshot.AgentProjections.Orchestrator.ManagerJobs.Count)

                let envelopes = (Boot.boot tempDir).Envelopes

                let publishedCount =
                    envelopes
                    |> Seq.filter (fun env ->
                        match env.Fact with
                        | Fact.Agent(AgentFact.OrchestratorPublished _) -> true
                        | _ -> false)
                    |> Seq.length

                Assert.Equal(1, publishedCount)

                // A second reconcile (e.g. a second restart) is idempotent: the
                // job is already Published, so nothing is appended again.
                let! second =
                    OrchestratorAuthority.reconcilePublishedFromAuthority
                        (Some journal)
                        (OrchestratorAuthority.createPort ())
                        repoDir
                        "main"

                Assert.Equal(0, second.Length)

                let envelopesAfter = (Boot.boot tempDir).Envelopes
                Assert.Equal(envelopes.Length, envelopesAfter.Length)
            })
