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
                        let! code2, stdout2, stderr2 =
                            OrchestratorGit.run (OrchestratorGit.command repoPath [ "rev-parse"; "HEAD" ])

                        if code2 = 0 && not (String.IsNullOrWhiteSpace stdout2) then
                            return Ok(stdout2.Trim())
                        else
                            return
                                Error(
                                    if String.IsNullOrWhiteSpace stderr then
                                        if String.IsNullOrWhiteSpace stderr2 then
                                            stdout2
                                        else
                                            stderr2
                                    else
                                        stderr
                                )
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
        : Task<unit> =
        task {
            match journal with
            | None -> return ()
            | Some journal ->
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

                                let fact =
                                    AgentFact.OrchestratorPublished
                                        {| ManagerId = ManagerId.value mgrId
                                           CandidateId = candId
                                           CommitHash = head |}

                                AgentJournal.appendAgent StreamId.Workspace None fact journal |> ignore
                        | _ -> ()
        }
