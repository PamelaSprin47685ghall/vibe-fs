namespace Wanxiangshu.Next.Journal

open System
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

    let private emptyGuard barrierKey : ReviewGuardProjection =
        { LastGitTreeHash = None
          ConsecutivePerfects = 0
          IsConfirmed = false
          AcceptedGuardKey = None
          RecentToolCallIds = []
          RecentProviderRunIds = []
          ConfirmationPhysicalMessageId = None
          AuthorityRootUserMessageId = None
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
               UserMessageId: string option
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

                            // Prefer physical confirmation message id. Keep confirmation-marker text as
                            // fail-soft fallback when Host physical id mapping is unavailable
                            // in the current tool context, but never accept a second PERFECT
                            // with neither identity proof.
                            let physicalConfirmationMatched =
                                match p.UserMessageId, existing.ConfirmationPhysicalMessageId with
                                | Some userMsg, Some confirmMsg when
                                    not (String.IsNullOrWhiteSpace userMsg)
                                    && not (String.IsNullOrWhiteSpace confirmMsg)
                                    && userMsg = confirmMsg
                                    ->
                                    true
                                | _ -> false

                            let confirmationPending =
                                match existing.AcceptedGuardKey with
                                | Some key when key.IndexOf("confirm-perfect", StringComparison.OrdinalIgnoreCase) >= 0 ->
                                    true
                                | _ -> existing.ConfirmationPhysicalMessageId.IsSome

                            let markerConfirmationMatched =
                                match p.UserPromptText with
                                | Some text when
                                    not (String.IsNullOrWhiteSpace text)
                                    && confirmationPending
                                    && text.IndexOf("PERFECT requires confirmation", StringComparison.Ordinal) >= 0
                                    ->
                                    true
                                | _ -> false

                            // Host tool context may not yet expose the physical confirmation
                            // message id. When confirm-perfect was accepted, a distinct second
                            // PERFECT provider run is accepted once while physical id remains
                            // the preferred proof when present.
                            let acceptedConfirmSecondPerfect =
                                confirmationPending
                                && existing.ConsecutivePerfects = 1
                                && not providerRunUsed

                            let secondPerfectConfirmed =
                                hasValidProviderRunId
                                && not providerRunUsed
                                && (physicalConfirmationMatched
                                    || markerConfirmationMatched
                                    || acceptedConfirmSecondPerfect)

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
                                        ConfirmationPhysicalMessageId = None
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
                                        ConfirmationPhysicalMessageId = None
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
                                        ConfirmationPhysicalMessageId = None
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
            // Guard prompts are sent to the reviewer child. Prefer the linked
            // parent manager that owns ReviewGuard finish state. Only keep the
            // target when it already holds the ReviewGuard (unit-test shape).
            let child = ChildId.create (SessionId.value p.TargetSessionId)

            let linkedParent =
                proj.Sessions
                |> Map.tryPick (fun parentId session ->
                    session.Linkage
                    |> Option.bind (fun linkage ->
                        if Map.containsKey child linkage.LinkedChildren then
                            Some parentId
                        else
                            None))

            match linkedParent with
            | Some parentId -> parentId
            | None ->
                match Map.tryFind p.TargetSessionId proj.Sessions with
                | Some session when session.ReviewGuard.IsSome -> p.TargetSessionId
                | _ -> p.TargetSessionId

        // Physical confirmation id only for the confirm-perfect path.
        // Missing-verdict / missing-review prompts do not authorize a second PERFECT.
        let isConfirmPerfect =
            p.GuardKey.IndexOf("confirm-perfect", StringComparison.OrdinalIgnoreCase) >= 0

        let hostMessageId =
            if String.IsNullOrWhiteSpace p.HostMessageId then None else Some p.HostMessageId

        let applyAcceptance (s: SessionAgentProjection) =
            let rg =
                match s.ReviewGuard with
                | Some existing ->
                    { existing with
                        AcceptedGuardKey = Some p.GuardKey
                        ConfirmationPhysicalMessageId =
                            if isConfirmPerfect then
                                hostMessageId
                            else
                                existing.ConfirmationPhysicalMessageId }
                | None ->
                    { emptyGuard None with
                        AcceptedGuardKey = Some p.GuardKey
                        ConfirmationPhysicalMessageId =
                            if isConfirmPerfect then hostMessageId else None }

            { s with ReviewGuard = Some rg }

        // Always update the resolved owner. Also update the target session so a
        // dual-write survives linkage gaps without inventing a second authority.
        let sessions =
            let withOwner = updateSession reviewOwner applyAcceptance proj.Sessions

            if reviewOwner = p.TargetSessionId then
                withOwner
            else
                updateSession p.TargetSessionId applyAcceptance withOwner

        { proj with Sessions = sessions }
