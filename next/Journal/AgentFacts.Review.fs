namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open AgentFactsFoldHelpers

module internal AgentFactsReview =

    /// Content marker embedded in the confirmation prompt. Second PERFECT is
    /// proven when the current user prompt contains this marker — not by host
    /// message ids.
    [<Literal>]
    let ConfirmationMarker = "PERFECT requires confirmation"

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

    let private emptyGuard barrierKey : ReviewGuardProjection =
        { LastGitTreeHash = None
          ConsecutivePerfects = 0
          IsConfirmed = false
          AcceptedGuardKey = None
          RecentToolCallIds = []
          RecentProviderRunIds = []
          ConfirmationPromptMarker = None
          CurrentBarrierKey = barrierKey }

    let foldReviewBarrierStarted
        (proj: AgentProjectionSet)
        (p:
            {| ManagerSessionId: SessionId
               BarrierKey: string |})
        : AgentProjectionSet =
        // A new review barrier resets the guard so the phase requires two FRESH
        // PERFECT verdicts on the current tree. The reset prevents a stale
        // confirmation from carrying across phases (e.g. pre-rebase into
        // post-rebase when the tree hash is unchanged by rebase).
        //
        // Idempotent per key: re-emitting the barrier that is already current is
        // a replay (restart resume), NOT a new phase.
        let sessions =
            updateSession
                p.ManagerSessionId
                (fun s ->
                    let rg =
                        match s.ReviewGuard with
                        | Some existing when existing.CurrentBarrierKey = Some p.BarrierKey -> existing
                        | Some _ -> emptyGuard (Some p.BarrierKey)
                        | None -> emptyGuard (Some p.BarrierKey)

                    { s with ReviewGuard = Some rg })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldReviewVerdictRecorded
        (proj: AgentProjectionSet)
        (p:
            {| ManagerSessionId: SessionId
               ReviewerSessionId: SessionId
               ProviderRunId: string
               UserPromptText: string option
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

                            let hasValidProviderRunId = not (String.IsNullOrWhiteSpace p.ProviderRunId)

                            // Content-based confirmation: the second PERFECT's user
                            // prompt must contain the confirmation marker that was
                            // set when ReviewGuard sent the confirmation request.
                            // No host message ids are compared.
                            let promptContainsConfirmation =
                                match p.UserPromptText, existing.ConfirmationPromptMarker with
                                | Some text, Some marker when
                                    not (String.IsNullOrWhiteSpace text)
                                    && not (String.IsNullOrWhiteSpace marker)
                                    && text.IndexOf(marker, StringComparison.Ordinal) >= 0
                                    ->
                                    true
                                | _ -> false

                            let secondPerfectConfirmed =
                                hasValidProviderRunId && not providerRunUsed && promptContainsConfirmation

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
                                        ConfirmationPromptMarker = None
                                        RecentToolCallIds = recentToolCallIds
                                        RecentProviderRunIds = recentProviderRunIds }
                            | _ ->
                                match p.Verdict with
                                | ReviewGuardVerdict.Perfect when not providerRunUsed ->
                                    { existing with
                                        LastGitTreeHash = Some hash
                                        ConsecutivePerfects = 1
                                        IsConfirmed = false
                                        // Tree changed: prior confirmation is stale.
                                        ConfirmationPromptMarker = None
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
                                        ConfirmationPromptMarker = None
                                        RecentToolCallIds = recentToolCallIds
                                        RecentProviderRunIds = recentProviderRunIds }
                        | None ->
                            match p.Verdict with
                            | ReviewGuardVerdict.Perfect ->
                                { emptyGuard None with
                                    LastGitTreeHash = Some hash
                                    ConsecutivePerfects = 1
                                    RecentToolCallIds = [ p.ToolCallId ]
                                    RecentProviderRunIds = [ p.ProviderRunId ] }
                            | ReviewGuardVerdict.Revise ->
                                { emptyGuard None with
                                    LastGitTreeHash = Some hash
                                    RecentToolCallIds = [ p.ToolCallId ]
                                    RecentProviderRunIds = [ p.ProviderRunId ] }

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
        // Manager/Orchestrator review state is owned by the review owner session.
        // A reviewer continuation is sent to the child session, so resolve its
        // linked parent before recording acceptance.
        let reviewOwner =
            match Map.tryFind p.TargetSessionId proj.Sessions with
            | Some session when session.ReviewGuard.IsSome -> p.TargetSessionId
            | _ ->
                let child = ChildId.create (SessionId.value p.TargetSessionId)

                proj.Sessions
                |> Map.tryPick (fun parentId session ->
                    session.Linkage
                    |> Option.bind (fun linkage ->
                        if Map.containsKey child linkage.LinkedChildren then
                            Some parentId
                        else
                            None))
                |> Option.defaultValue p.TargetSessionId

        // Content marker only for the confirm-perfect path. Missing-verdict /
        // missing-review prompts do not authorize a second PERFECT.
        let isConfirmPerfect =
            p.GuardKey.IndexOf("confirm-perfect", StringComparison.OrdinalIgnoreCase) >= 0

        let sessions =
            updateSession
                reviewOwner
                (fun s ->
                    let rg =
                        match s.ReviewGuard with
                        | Some existing ->
                            { existing with
                                AcceptedGuardKey = Some p.GuardKey
                                ConfirmationPromptMarker =
                                    if isConfirmPerfect then
                                        Some ConfirmationMarker
                                    else
                                        existing.ConfirmationPromptMarker }
                        | None ->
                            { emptyGuard None with
                                AcceptedGuardKey = Some p.GuardKey
                                ConfirmationPromptMarker = if isConfirmPerfect then Some ConfirmationMarker else None }

                    { s with ReviewGuard = Some rg })
                proj.Sessions

        { proj with Sessions = sessions }
