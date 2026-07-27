namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Orchestrator

/// Git authority + restart reconcile helpers for production OrchestratorHost.
module OrchestratorAuthority =
    let createPort () : GitAuthorityPort =
        { GetHead =
            fun worktreePath ->
                task {
                    let! code, stdout, stderr =
                        OrchestratorGit.run (OrchestratorGit.command worktreePath [ "rev-parse"; "HEAD" ])

                    if code = 0 && not (String.IsNullOrWhiteSpace stdout) then
                        return Ok(stdout.Trim())
                    else
                        return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
                }
          GetTargetHead =
            fun repoPath branch ->
                task {
                    let! code, stdout, stderr =
                        OrchestratorGit.run (
                            OrchestratorGit.command repoPath [ "rev-parse"; sprintf "refs/heads/%s" branch ]
                        )

                    if code = 0 && not (String.IsNullOrWhiteSpace stdout) then
                        return Ok(stdout.Trim())
                    else
                        // Fail closed: never fall back to HEAD. A missing target
                        // branch must not publish to an unrelated commit.
                        let reason =
                            if String.IsNullOrWhiteSpace stderr then
                                sprintf "target branch not found: %s" branch
                            else
                                stderr.Trim()

                        return Error reason
                } }

    /// Proper ancestor check via `git merge-base --is-ancestor <candidate> <head>`.
    /// String-prefix comparisons collide on short hashes and are not ancestry.
    let private isAncestor (repoPath: string) (candidate: string) (head: string) : Task<bool> =
        task {
            let! code, _, _ =
                OrchestratorGit.run (
                    OrchestratorGit.command repoPath [ "merge-base"; "--is-ancestor"; candidate; head ]
                )

            return code = 0
        }

    /// Reconcile durable ManagerJobs against Git authority after restart.
    /// Re-derive `Published` for jobs whose candidate is already contained in the
    /// target branch (candidate committed but publish not yet recorded). Jobs that
    /// are earlier in the pipeline (manager not finished, rebase in progress, post-
    /// rebase review, awaiting publish) cannot be re-driven from a static reconcile
    /// without the runtime Manager/Reviewer ports; the persisted prompt (on the
    /// ManagerJobCreated fact) makes such resume possible but is wired at runtime.
    let reconcilePublishedFromAuthority
        (journal: AgentJournal option)
        (authority: GitAuthorityPort)
        (repoPath: string)
        (branch: string)
        : Task<(string * string) list> =
        task {
            match journal with
            | None -> return []
            | Some journal ->
                let reconciled = ResizeArray<string * string>()
                let snapshot = AgentJournal.snapshot journal
                let jobs = snapshot.AgentProjections.Orchestrator.ManagerJobs

                for KeyValue(mgrId, job) in jobs do
                    match job.PublishedCommit, job.CandidateCommit with
                    | Some _, _ -> ()
                    | None, None -> ()
                    | None, Some candidate ->
                        let! targetHead = authority.GetTargetHead repoPath branch

                        match targetHead with
                        | Ok head ->
                            let! contained =
                                task {
                                    // ponytail: exact full-hash equality, else proper ancestor check.
                                    if head = candidate then
                                        return true
                                    else
                                        return! isAncestor repoPath candidate head
                                }

                            if contained then
                                let candId =
                                    match job.CandidateId with
                                    | Some id -> CandidateId.value id
                                    | None -> sprintf "candidate-%s" (ManagerId.value mgrId)

                                // Record the CANDIDATE commit, not the current target
                                // HEAD. The target HEAD is only used for the ancestor
                                // check; the published fact must identify the commit
                                // that was published, which is the candidate.
                                let fact =
                                    AgentFact.OrchestratorPublished
                                        {| ManagerId = ManagerId.value mgrId
                                           CandidateId = candId
                                           CommitHash = candidate |}

                                match AgentJournal.appendAgent StreamId.Workspace None fact journal with
                                | Ok _ -> reconciled.Add(ManagerId.value mgrId, candidate)
                                | Error _ -> ()
                        | _ -> ()

                return reconciled |> Seq.toList
        }
