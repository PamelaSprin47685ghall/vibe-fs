namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open AgentFactsFoldHelpers

module internal AgentFactsReview =

    let private appendRecentToolCallId (ids: string list) (toolCallId: string) =
        if List.contains toolCallId ids then
            ids
        else
            ids @ [ toolCallId ]

    let foldReviewVerdictRecorded
        (proj: AgentProjectionSet)
        (p:
            {| ManagerSessionId: SessionId
               ReviewerSessionId: SessionId
               ToolCallId: string
               GitTreeHash: string
               Verdict: ReviewGuardVerdict |})
        : AgentProjectionSet =
        let hash = GitTreeHash.create p.GitTreeHash

        let sessions =
            updateSession
                p.ManagerSessionId
                (fun s ->
                    let rg =
                        match s.ReviewGuard with
                        | Some existing when List.contains p.ToolCallId existing.RecentToolCallIds -> existing
                        | Some existing ->
                            let recentToolCallIds =
                                appendRecentToolCallId existing.RecentToolCallIds p.ToolCallId

                            match existing.LastGitTreeHash with
                            | Some lastHash when lastHash = hash ->
                                match p.Verdict with
                                | ReviewGuardVerdict.Perfect ->
                                    let count = existing.ConsecutivePerfects + 1

                                    { existing with
                                        LastGitTreeHash = Some hash
                                        ConsecutivePerfects = count
                                        IsConfirmed = count >= 2
                                        RecentToolCallIds = recentToolCallIds }
                                | ReviewGuardVerdict.Revise ->
                                    { existing with
                                        LastGitTreeHash = Some hash
                                        ConsecutivePerfects = 0
                                        IsConfirmed = false
                                        RecentToolCallIds = recentToolCallIds }
                            | _ ->
                                match p.Verdict with
                                | ReviewGuardVerdict.Perfect ->
                                    { existing with
                                        LastGitTreeHash = Some hash
                                        ConsecutivePerfects = 1
                                        IsConfirmed = false
                                        RecentToolCallIds = recentToolCallIds }
                                | ReviewGuardVerdict.Revise ->
                                    { existing with
                                        LastGitTreeHash = Some hash
                                        ConsecutivePerfects = 0
                                        IsConfirmed = false
                                        RecentToolCallIds = recentToolCallIds }
                        | None ->
                            match p.Verdict with
                            | ReviewGuardVerdict.Perfect ->
                                { LastGitTreeHash = Some hash
                                  ConsecutivePerfects = 1
                                  IsConfirmed = false
                                  AcceptedGuardKeys = Set.empty
                                  RecentToolCallIds = [ p.ToolCallId ] }
                            | ReviewGuardVerdict.Revise ->
                                { LastGitTreeHash = Some hash
                                  ConsecutivePerfects = 0
                                  IsConfirmed = false
                                  AcceptedGuardKeys = Set.empty
                                  RecentToolCallIds = [ p.ToolCallId ] }

                    { s with ReviewGuard = Some rg })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldGuardPromptAccepted
        (proj: AgentProjectionSet)
        (p:
            {| TargetSessionId: SessionId
               GuardKey: string
               HostMessageId: string |})
        : AgentProjectionSet =
        let sessions =
            updateSession
                p.TargetSessionId
                (fun s ->
                    let rg =
                        match s.ReviewGuard with
                        | Some existing ->
                            { existing with
                                AcceptedGuardKeys = Set.add p.GuardKey existing.AcceptedGuardKeys }
                        | None ->
                            { LastGitTreeHash = None
                              ConsecutivePerfects = 0
                              IsConfirmed = false
                              AcceptedGuardKeys = Set.singleton p.GuardKey
                              RecentToolCallIds = [] }

                    { s with ReviewGuard = Some rg })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldFallbackFailureRecorded
        (proj: AgentProjectionSet)
        (p:
            {| SessionId: SessionId
               Reason: string |})
        : AgentProjectionSet =
        let sessions =
            updateSession
                p.SessionId
                (fun s ->
                    let fb =
                        match s.Fallback with
                        | Some existing ->
                            if existing.IsDead then
                                existing
                            else
                                let newTotal = existing.TotalFailures + 1

                                match existing.Side with
                                | SideA ->
                                    if existing.FailuresOnCurrentSide < 1 then
                                        { Side = SideA
                                          FailuresOnCurrentSide = existing.FailuresOnCurrentSide + 1
                                          TotalFailures = newTotal
                                          IsDead = false }
                                    else
                                        { Side = SideB
                                          FailuresOnCurrentSide = 0
                                          TotalFailures = newTotal
                                          IsDead = false }
                                | SideB ->
                                    if existing.FailuresOnCurrentSide < 1 then
                                        { Side = SideB
                                          FailuresOnCurrentSide = existing.FailuresOnCurrentSide + 1
                                          TotalFailures = newTotal
                                          IsDead = false }
                                    else
                                        { Side = SideB
                                          FailuresOnCurrentSide = 2
                                          TotalFailures = newTotal
                                          IsDead = true }
                        | None ->
                            { Side = SideA
                              FailuresOnCurrentSide = 1
                              TotalFailures = 1
                              IsDead = false }

                    { s with Fallback = Some fb })
                proj.Sessions

        { proj with Sessions = sessions }
