namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open AgentFactsFoldHelpers

module internal AgentFactsReview =

    let private recentToolCallWindowSize = 2

    let private appendRecentToolCallId (ids: string list) (toolCallId: string) =
        if List.contains toolCallId ids then
            ids
        else
            let updated = ids @ [ toolCallId ]

            if List.length updated > recentToolCallWindowSize then
                List.skip (List.length updated - recentToolCallWindowSize) updated
            else
                updated

    let foldReviewBarrierStarted
        (proj: AgentProjectionSet)
        (p:
            {| ManagerSessionId: SessionId
               BarrierKey: string |})
        : AgentProjectionSet =
        // A new review barrier resets the guard so the phase requires two FRESH
        // PERFECT verdicts (distinct ToolCallIds) on the current tree. The
        // barrier key is recorded for auditability; the reset itself is what
        // prevents a stale confirmation from carrying across phases (e.g.
        // pre-rebase confirmation borrowing into post-rebase when the tree
        // hash is unchanged by rebase).
        let sessions =
            updateSession
                p.ManagerSessionId
                (fun s ->
                    let rg =
                        match s.ReviewGuard with
                        | Some existing ->
                            { existing with
                                // Clearing the tree hash is load-bearing: the
                                // review-state reader treats "same tree + zero
                                // perfects" as REVISE. A fresh barrier must read
                                // as "no verdicts yet" even when the tree is
                                // unchanged (rebase alters ancestry, not tree).
                                LastGitTreeHash = None
                                ConsecutivePerfects = 0
                                IsConfirmed = false
                                RecentToolCallIds = []
                                CurrentBarrierKey = Some p.BarrierKey }
                        | None ->
                            { LastGitTreeHash = None
                              ConsecutivePerfects = 0
                              IsConfirmed = false
                              AcceptedGuardKey = None
                              RecentToolCallIds = []
                              CurrentBarrierKey = Some p.BarrierKey }

                    { s with ReviewGuard = Some rg })
                proj.Sessions

        { proj with Sessions = sessions }

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
                                  AcceptedGuardKey = None
                                  RecentToolCallIds = [ p.ToolCallId ]
                                  CurrentBarrierKey = None }
                            | ReviewGuardVerdict.Revise ->
                                { LastGitTreeHash = Some hash
                                  ConsecutivePerfects = 0
                                  IsConfirmed = false
                                  AcceptedGuardKey = None
                                  RecentToolCallIds = [ p.ToolCallId ]
                                  CurrentBarrierKey = None }

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
                                AcceptedGuardKey = Some p.GuardKey }
                        | None ->
                            { LastGitTreeHash = None
                              ConsecutivePerfects = 0
                              IsConfirmed = false
                              AcceptedGuardKey = Some p.GuardKey
                              RecentToolCallIds = []
                              CurrentBarrierKey = None }

                    { s with ReviewGuard = Some rg })
                proj.Sessions

        { proj with Sessions = sessions }

    let private failureIdentity (assistantMessageId: string) (providerAttempt: string) =
        sprintf "%s|%s" assistantMessageId providerAttempt

    let private rememberFailureId (ids: string list) (identity: string) =
        let next = identity :: (ids |> List.filter ((<>) identity))
        // Bounded projection: keep only the most recent identities. 4 covers
        // the full A/A/B/B sequence (the Dead threshold is 4 failures), so a
        // dead session's identities are retained for cross-restart dedup while
        // live sessions never grow unbounded.
        next |> List.truncate 4

    let foldFallbackFailureRecorded
        (proj: AgentProjectionSet)
        (p:
            {| SessionId: SessionId
               Reason: string
               AssistantMessageId: string
               ProviderAttempt: string |})
        : AgentProjectionSet =
        let identity = failureIdentity p.AssistantMessageId p.ProviderAttempt

        let sessions =
            updateSession
                p.SessionId
                (fun s ->
                    let fb =
                        match s.Fallback with
                        | Some existing when List.contains identity existing.RecentFailureIds -> existing
                        | Some existing when existing.IsDead ->
                            // Defense-in-depth: the append boundary already refuses
                            // new FallbackFailureRecorded facts for dead sessions.
                            // If a fact still reaches the fold (e.g. replayed from
                            // a prior journal), return the existing projection
                            // unchanged — a dead session never accumulates more.
                            existing
                        | Some existing ->
                            let newTotal = existing.TotalFailures + 1
                            let ids = rememberFailureId existing.RecentFailureIds identity

                            match existing.Side with
                            | SideA ->
                                if existing.FailuresOnCurrentSide < 1 then
                                    { Side = SideA
                                      FailuresOnCurrentSide = existing.FailuresOnCurrentSide + 1
                                      TotalFailures = newTotal
                                      IsDead = false
                                      RecentFailureIds = ids }
                                else
                                    { Side = SideB
                                      FailuresOnCurrentSide = 0
                                      TotalFailures = newTotal
                                      IsDead = false
                                      RecentFailureIds = ids }
                            | SideB ->
                                if existing.FailuresOnCurrentSide < 1 then
                                    { Side = SideB
                                      FailuresOnCurrentSide = existing.FailuresOnCurrentSide + 1
                                      TotalFailures = newTotal
                                      IsDead = false
                                      RecentFailureIds = ids }
                                else
                                    { Side = SideB
                                      FailuresOnCurrentSide = 2
                                      TotalFailures = newTotal
                                      IsDead = true
                                      RecentFailureIds = ids }
                        | None ->
                            { Side = SideA
                              FailuresOnCurrentSide = 1
                              TotalFailures = 1
                              IsDead = false
                              RecentFailureIds = [ identity ] }

                    { s with Fallback = Some fb })
                proj.Sessions

        { proj with Sessions = sessions }
