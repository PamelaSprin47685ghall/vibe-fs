namespace Wanxiangshu.Next.Review

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

type GitPort = { GetTreeHash: unit -> string }

type HostPort =
    { SendGuardPrompt: SessionId -> string -> string -> Result<string, string> }

type JournalPort =
    { AppendFact: StreamId -> AgentFact -> Result<ProjectionSet, string> }

module JournalPort =
    let fromAgentJournal (journal: AgentJournal) : JournalPort =
        { AppendFact =
            fun stream fact ->
                match AgentJournal.appendAgent stream None fact journal with
                | Ok proj -> Ok proj
                | Error failure -> Error(sprintf "%A" failure.Failure) }

module Guard =

    let recordVerdict
        (journalPort: JournalPort)
        (managerSessionId: SessionId)
        (reviewerSessionId: SessionId)
        (toolCallId: string)
        (gitTreeHash: string)
        (verdict: ReviewGuardVerdict)
        : Result<ProjectionSet, string> =
        let fact =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = managerSessionId
                   ReviewerSessionId = reviewerSessionId
                   ToolCallId = toolCallId
                   GitTreeHash = gitTreeHash
                   Verdict = verdict |}

        journalPort.AppendFact (StreamId.Session managerSessionId) fact

    let tryFinish (gitPort: GitPort) (managerSessionId: SessionId) (projSet: ProjectionSet) : ReviewFinishResult =
        let currentHash = gitPort.GetTreeHash()

        match Map.tryFind managerSessionId projSet.AgentProjections.Sessions with
        | Some sessionProj ->
            match sessionProj.ReviewGuard with
            | Some rg ->
                match rg.LastGitTreeHash with
                | Some lastHash when GitTreeHash.value lastHash = currentHash ->
                    if rg.IsConfirmed then
                        ReviewFinishResult.Confirmed
                    else
                        ReviewFinishResult.NeedsReview
                | _ -> ReviewFinishResult.NeedsReview
            | None -> ReviewFinishResult.NeedsReview
        | None -> ReviewFinishResult.NeedsReview

    let guardMissingVerdict
        (hostPort: HostPort)
        (journalPort: JournalPort)
        (targetSessionId: SessionId)
        (guardKey: string)
        (promptText: string)
        (projSet: ProjectionSet)
        : Result<ProjectionSet * string option, string> =
        let alreadyAccepted =
            match Map.tryFind targetSessionId projSet.AgentProjections.Sessions with
            | Some s ->
                match s.ReviewGuard with
                | Some rg -> Set.contains guardKey rg.AcceptedGuardKeys
                | None -> false
            | None -> false

        if alreadyAccepted then
            Ok(projSet, None)
        else
            match hostPort.SendGuardPrompt targetSessionId guardKey promptText with
            | Ok hostMsgId ->
                let fact =
                    AgentFact.GuardPromptAccepted
                        {| TargetSessionId = targetSessionId
                           GuardKey = guardKey
                           HostMessageId = hostMsgId |}

                match journalPort.AppendFact (StreamId.Session targetSessionId) fact with
                | Ok updatedProj -> Ok(updatedProj, Some hostMsgId)
                | Error err -> Error err
            | Error err -> Error err
