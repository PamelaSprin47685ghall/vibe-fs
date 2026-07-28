namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

type ReviewerHost
    (journal: AgentJournal, managerSessionId: SessionId, reviewerSessionId: SessionId, ?gitTreePort: GitTreePort) =
    let gate = obj ()

    let reviewState (projection: ProjectionSet) (treeHash: string) =
        match Map.tryFind managerSessionId projection.AgentProjections.Sessions with
        | Some session ->
            match session.ReviewGuard with
            | Some guard when guard.IsConfirmed && guard.LastGitTreeHash = Some(GitTreeHash.create treeHash) ->
                ReviewFinishResult.Confirmed
            | _ -> ReviewFinishResult.NeedsReview
        | None -> ReviewFinishResult.NeedsReview

    /// providerRunId must be per-call. Prefer Host assistant message id (one run);
    /// fall back to toolCallId only when the host does not expose a run id.
    /// userPromptText is the current user prompt content used for content-based
    /// second-PERFECT confirmation (no host message ids).
    member _.RecordVerdict
        (
            toolCallId: string,
            treeHash: string,
            verdict: ReviewGuardVerdict,
            providerRunId: string,
            ?userPromptText: string
        ) : Result<ReviewFinishResult, string> =
        if System.String.IsNullOrWhiteSpace providerRunId then
            Error "ReviewerHost.RecordVerdict requires a real ProviderRunId"
        else
            let promptText =
                match userPromptText with
                | Some text when not (System.String.IsNullOrWhiteSpace text) -> Some text
                | _ -> None

            lock gate (fun () ->
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
                               ProviderRunId = providerRunId
                               UserPromptText = promptText
                               ToolCallId = toolCallId
                               GitTreeHash = treeHash
                               Verdict = verdict |}

                    match AgentJournal.appendAgent (StreamId.Session managerSessionId) None fact journal with
                    | Ok updated -> Ok(reviewState updated treeHash)
                    | Error failure -> Error(sprintf "%A" failure.Failure))

    member this.SubmitVerdict
        (toolCallId: string, verdict: ReviewGuardVerdict, ?providerRunId: string, ?userPromptText: string)
        : Result<ReviewFinishResult, string> =
        match providerRunId with
        | None
        | Some "" -> Error "ReviewerHost.SubmitVerdict requires a real ProviderRunId"
        | Some runId ->
            match gitTreePort with
            | Some port ->
                this.RecordVerdict(toolCallId, port.GetTreeHash(), verdict, runId, ?userPromptText = userPromptText)
            | None -> Error "ReviewerHost.SubmitVerdict requires a GitTreePort"

    member _.TryFinish(currentTreeHash: string) =
        reviewState (AgentJournal.snapshot journal) currentTreeHash

    member _.TryFinish() =
        match gitTreePort with
        | Some port -> reviewState (AgentJournal.snapshot journal) (port.GetTreeHash())
        | None -> ReviewFinishResult.NeedsReview
