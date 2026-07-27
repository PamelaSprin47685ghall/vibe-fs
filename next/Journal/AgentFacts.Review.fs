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
        // PERFECT verdicts (distinct ToolCallIds) on the current tree. The reset
        // prevents a stale confirmation from carrying across phases (e.g.
        // pre-rebase confirmation borrowing into post-rebase when the tree hash
        // is unchanged by rebase).
        //
        // Idempotent per key: re-emitting the barrier that is already current is
        // a replay (restart resume re-runs the same phase), NOT a new phase. It
        // must not discard durable verdicts already recorded for this barrier,
        // which would burn two extra reviewer rounds after every restart.
        let sessions =
            updateSession
                p.ManagerSessionId
                (fun s ->
                    let rg =
                        match s.ReviewGuard with
                        | Some existing when existing.CurrentBarrierKey = Some p.BarrierKey -> existing
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
                                RecentProviderRunIds = []
                                ConfirmationPromptMessageId = None
                                CurrentBarrierKey = Some p.BarrierKey }
                        | None ->
                            { LastGitTreeHash = None
                              ConsecutivePerfects = 0
                              IsConfirmed = false
                              AcceptedGuardKey = None
                              RecentToolCallIds = []
                              RecentProviderRunIds = []
                              ConfirmationPromptMessageId = None
                              CurrentBarrierKey = Some p.BarrierKey }

                    { s with ReviewGuard = Some rg })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldReviewVerdictRecorded
        (proj: AgentProjectionSet)
        (p:
            {| ManagerSessionId: SessionId
               ReviewerSessionId: SessionId
               ProviderRunId: string
               RootUserMessageId: string option
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

                            let providerRunUsed = List.contains p.ProviderRunId existing.RecentProviderRunIds

                            let recentProviderRunIds =
                                if not providerRunUsed then
                                    appendRecentToolCallId existing.RecentProviderRunIds p.ProviderRunId
                                else
                                    existing.RecentProviderRunIds

                            let hasValidProviderRunId = not (System.String.IsNullOrWhiteSpace p.ProviderRunId)

                            // Fail-closed causal proof: the second PERFECT's root user
                            // message must equal the confirmation prompt ReviewGuard sent
                            // after the first PERFECT (KISS-N07 normative). A missing
                            // ConfirmationPromptMessageId means no confirmation nudge has
                            // landed yet (e.g. host send still in flight, or the nudge was
                            // never wired for this session) -- that must NOT auto-pass, or
                            // any two independent PERFECT calls on an unchanged tree would
                            // confirm without proof of a real confirmation round-trip.
                            let secondPerfectConfirmed =
                                hasValidProviderRunId
                                && not providerRunUsed
                                && match p.RootUserMessageId, existing.ConfirmationPromptMessageId with
                                   | Some rootId, Some confirmId -> rootId = confirmId
                                   | _ -> false

                            match existing.LastGitTreeHash with
                            | Some lastHash when lastHash = hash ->
                                match p.Verdict with
                                | ReviewGuardVerdict.Perfect when existing.ConsecutivePerfects >= 1 ->
                                    if secondPerfectConfirmed then
                                        { existing with
                                            LastGitTreeHash = Some hash
                                            ConsecutivePerfects = 2
                                            IsConfirmed = true
                                            RecentToolCallIds = recentToolCallIds
                                            RecentProviderRunIds = recentProviderRunIds }
                                    else
                                        { existing with
                                            LastGitTreeHash = Some hash
                                            IsConfirmed = false
                                            RecentToolCallIds = recentToolCallIds
                                            RecentProviderRunIds = recentProviderRunIds }
                                | ReviewGuardVerdict.Perfect when not providerRunUsed ->
                                    { existing with
                                        LastGitTreeHash = Some hash
                                        ConsecutivePerfects = 1
                                        IsConfirmed = false
                                        RecentToolCallIds = recentToolCallIds
                                        RecentProviderRunIds = recentProviderRunIds }
                                | ReviewGuardVerdict.Perfect ->
                                    { existing with
                                        LastGitTreeHash = Some hash
                                        RecentToolCallIds = recentToolCallIds
                                        RecentProviderRunIds = recentProviderRunIds }
                                | ReviewGuardVerdict.Revise ->
                                    { existing with
                                        LastGitTreeHash = Some hash
                                        ConsecutivePerfects = 0
                                        IsConfirmed = false
                                        ConfirmationPromptMessageId = None
                                        RecentToolCallIds = recentToolCallIds
                                        RecentProviderRunIds = recentProviderRunIds }
                            | _ ->
                                match p.Verdict with
                                | ReviewGuardVerdict.Perfect when not providerRunUsed ->
                                    { existing with
                                        LastGitTreeHash = Some hash
                                        ConsecutivePerfects = 1
                                        IsConfirmed = false
                                        // Tree changed: any confirmation prompt issued for the
                                        // previous tree is stale and must not be reused to
                                        // confirm a PERFECT on this new tree.
                                        ConfirmationPromptMessageId = None
                                        RecentToolCallIds = recentToolCallIds
                                        RecentProviderRunIds = recentProviderRunIds }
                                | ReviewGuardVerdict.Perfect ->
                                    { existing with
                                        LastGitTreeHash = Some hash
                                        RecentToolCallIds = recentToolCallIds
                                        RecentProviderRunIds = recentProviderRunIds }
                                | ReviewGuardVerdict.Revise ->
                                    { existing with
                                        LastGitTreeHash = Some hash
                                        ConsecutivePerfects = 0
                                        IsConfirmed = false
                                        ConfirmationPromptMessageId = None
                                        RecentToolCallIds = recentToolCallIds
                                        RecentProviderRunIds = recentProviderRunIds }
                        | None ->
                            match p.Verdict with
                            | ReviewGuardVerdict.Perfect ->
                                { LastGitTreeHash = Some hash
                                  ConsecutivePerfects = 1
                                  IsConfirmed = false
                                  AcceptedGuardKey = None
                                  RecentToolCallIds = [ p.ToolCallId ]
                                  RecentProviderRunIds = [ p.ProviderRunId ]
                                  ConfirmationPromptMessageId = None
                                  CurrentBarrierKey = None }
                            | ReviewGuardVerdict.Revise ->
                                { LastGitTreeHash = Some hash
                                  ConsecutivePerfects = 0
                                  IsConfirmed = false
                                  AcceptedGuardKey = None
                                  RecentToolCallIds = [ p.ToolCallId ]
                                  RecentProviderRunIds = [ p.ProviderRunId ]
                                  ConfirmationPromptMessageId = None
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
                                AcceptedGuardKey = Some p.GuardKey
                                ConfirmationPromptMessageId = Some p.HostMessageId }
                        | None ->
                            { LastGitTreeHash = None
                              ConsecutivePerfects = 0
                              IsConfirmed = false
                              AcceptedGuardKey = Some p.GuardKey
                              RecentToolCallIds = []
                              RecentProviderRunIds = []
                              ConfirmationPromptMessageId = Some p.HostMessageId
                              CurrentBarrierKey = None }

                    { s with ReviewGuard = Some rg })
                proj.Sessions

        { proj with Sessions = sessions }
