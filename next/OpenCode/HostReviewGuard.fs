namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module HostReviewGuard =

    type ReviewGuardAvailability =
        | ReviewGuardMissing of treeHash: string
        | ReviewGuardConfirmed
        | ReviewGuardUnavailable of reason: string

    let missingTree (journal: AgentJournal option) (gitTreePort: GitTreePort option) sessionId =
        match journal, gitTreePort with
        | None, _ -> ReviewGuardUnavailable "Review guard requires an AgentJournal"
        | _, None -> ReviewGuardUnavailable "Review guard requires a GitTreePort"
        | Some journal, Some port ->
            try
                let treeHash = port.GetTreeHash()

                if String.IsNullOrWhiteSpace treeHash then
                    ReviewGuardUnavailable "Review guard GitTreePort returned an empty tree hash"
                else
                    let confirmed =
                        AgentJournal.snapshot journal
                        |> fun projection ->
                            match Map.tryFind (SessionId.create sessionId) projection.AgentProjections.Sessions with
                            | Some session ->
                                match session.ReviewGuard with
                                | Some guard ->
                                    guard.IsConfirmed && guard.LastGitTreeHash = Some(GitTreeHash.create treeHash)
                                | None -> false
                            | None -> false

                    if confirmed then
                        ReviewGuardConfirmed
                    else
                        ReviewGuardMissing treeHash
            with ex ->
                ReviewGuardUnavailable(sprintf "Review guard dependency failed: %s" ex.Message)

    let private hasAcceptedGuardKey (journal: AgentJournal) (targetSessionId: SessionId) (guardKey: string) =
        AgentJournal.snapshot journal
        |> fun projection ->
            match Map.tryFind targetSessionId projection.AgentProjections.Sessions with
            | Some session ->
                match session.ReviewGuard with
                | Some guard -> guard.AcceptedGuardKey = Some guardKey
                | None -> false
            | None -> false

    let private createGuardKey (targetSessionId: SessionId) (triggerMessageId: string) (reason: string) =
        sprintf "review-guard:%s:%s:%s" (SessionId.value targetSessionId) triggerMessageId reason

    let private sendGuardNudge
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        (sessionId: string)
        (triggerMessageId: string)
        (reason: string)
        (prompt: string)
        (agent: string)
        =
        let journal =
            match journal with
            | Some journal -> journal
            | None -> raise (InvalidOperationException "Review guard nudge requires an AgentJournal")

        let targetSessionId = SessionId.create sessionId
        let guardKey = createGuardKey targetSessionId triggerMessageId reason

        if
            not (hasAcceptedGuardKey journal targetSessionId guardKey)
            && nudgeKeys.Add guardKey
        then
            HostSessionNudge.send
                sessionPort
                targetSessionId
                prompt
                { Model = None; Agent = Some agent }
                (fun hostMessageId ->
                    let fact =
                        AgentFact.GuardPromptAccepted
                            {| TargetSessionId = targetSessionId
                               GuardKey = guardKey
                               HostMessageId = MessageId.value hostMessageId |}

                    match AgentJournal.appendAgent (StreamId.Session targetSessionId) None fact journal with
                    | Ok _ -> ()
                    | Error failure ->
                        raise (
                            InvalidOperationException(
                                sprintf "Failed to persist review guard prompt acceptance: %A" failure.Failure
                            )
                        ))

    let nudgeManager
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        sessionId
        messageId
        treeHash
        =
        sendGuardNudge
            sessionPort
            journal
            nudgeKeys
            sessionId
            messageId
            (sprintf "missing-review:%s" treeHash)
            "Review is required before completion. Fork or nudge a Reviewer until the current Git tree has two distinct PERFECT verdicts."
            "manager"

    let nudgeReviewer
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        sessionId
        messageId
        =
        sendGuardNudge
            sessionPort
            journal
            nudgeKeys
            sessionId
            messageId
            "missing-verdict"
            "Submit a structured verdict with the verdict tool: PERFECT or REVISE. Do not put a verdict in prose."
            "reviewer"
