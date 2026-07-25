namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module HostReviewGuard =

    let missingTree (journal: AgentJournal option) (gitTreePort: GitTreePort option) sessionId =
        match journal, gitTreePort with
        | Some journal, Some port ->
            let treeHash = port.GetTreeHash()

            let confirmed =
                AgentJournal.snapshot journal
                |> fun projection ->
                    match Map.tryFind (SessionId.create sessionId) projection.AgentProjections.Sessions with
                    | Some session ->
                        match session.ReviewGuard with
                        | Some guard -> guard.IsConfirmed && guard.LastGitTreeHash = Some(GitTreeHash.create treeHash)
                        | None -> false
                    | None -> false

            if confirmed then None else Some treeHash
        | _ -> None

    let nudgeManager
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        sessionId
        messageId
        treeHash
        =
        let guardKey = $"review:{treeHash}:{messageId}"

        if nudgeKeys.Add guardKey then
            HostSessionNudge.send
                sessionPort
                (SessionId.create sessionId)
                "Review is required before completion. Fork or nudge a Reviewer until the current Git tree has two distinct PERFECT verdicts."
                { Model = None; Agent = Some "manager" }
                (fun hostMessageId ->
                    match journal with
                    | Some journal ->
                        let fact =
                            AgentFact.GuardPromptAccepted
                                {| TargetSessionId = SessionId.create sessionId
                                   GuardKey = guardKey
                                   HostMessageId = MessageId.value hostMessageId |}

                        AgentJournal.appendAgent (StreamId.Session(SessionId.create sessionId)) None fact journal
                        |> ignore
                    | None -> ())
