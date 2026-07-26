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

    /// Reconcile durable ManagerJobs against Git authority after restart:
    /// append missing Published when target already contains the candidate.
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
                        | Ok head when head = candidate || head.StartsWith(candidate) || candidate.StartsWith(head) ->
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
