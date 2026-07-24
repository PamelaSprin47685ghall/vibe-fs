namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

type ReviewerHost(journal: AgentJournal, managerSessionId: SessionId, reviewerSessionId: SessionId) =
    let reviewState (projection: ProjectionSet) (treeHash: string) =
        match Map.tryFind managerSessionId projection.AgentProjections.Sessions with
        | Some session ->
            match session.ReviewGuard with
            | Some guard when guard.IsConfirmed && guard.LastGitTreeHash = Some(GitTreeHash.create treeHash) ->
                ReviewFinishResult.Confirmed
            | _ -> ReviewFinishResult.NeedsReview
        | None -> ReviewFinishResult.NeedsReview

    member _.RecordVerdict
        (toolCallId: string, treeHash: string, verdict: ReviewGuardVerdict)
        : Result<ReviewFinishResult, string> =
        let current = AgentJournal.snapshot journal

        let duplicate =
            match Map.tryFind managerSessionId current.AgentProjections.Sessions with
            | Some session ->
                session.ReviewGuard
                |> Option.exists (fun guard -> List.contains toolCallId guard.RecentToolCallIds)
            | None -> false

        if duplicate then
            Ok(reviewState current treeHash)
        else
            let fact =
                AgentFact.ReviewVerdictRecorded
                    {| ManagerSessionId = managerSessionId
                       ReviewerSessionId = reviewerSessionId
                       ToolCallId = toolCallId
                       GitTreeHash = treeHash
                       Verdict = verdict |}

            match AgentJournal.appendAgent (StreamId.Session managerSessionId) None fact journal with
            | Ok updated -> Ok(reviewState updated treeHash)
            | Error failure -> Error(sprintf "%A" failure.Failure)

    member _.TryFinish(currentTreeHash: string) =
        reviewState (AgentJournal.snapshot journal) currentTreeHash
