namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module HostReviewGuard =

    type ReviewGuardAvailability =
        | ReviewGuardMissing of treeHash: string
        | ReviewGuardConfirmed
        | ReviewGuardNotRequired
        | ReviewGuardUnavailable of reason: string

    /// What a guard nudge send resolved to. `AlreadyOutstanding` is not a failure:
    /// the in-flight claim still drives the next turn. `Failed` means nothing is
    /// in flight, so a deferred completion must not be left waiting on a nudge
    /// that never landed.
    [<RequireQualifiedAccess>]
    type GuardNudgeOutcome =
        | Sent of PromptKey
        | AlreadyOutstanding
        | NoLongerRequired
        | Failed of reason: string

    /// REVIEW-003's shared barrier writer lives in `ReviewBarrier.openBarrier`
    /// (Journal layer, compiled before the fork paths). Both the Orchestrator's
    /// review barrier (ORCH-006) and a Manager's own guard-path review fork
    /// (REVIEW-007) emit the same fact through the same function.
    let openBarrier = ReviewBarrier.openBarrier

    let private managerWorkRequiresGuard snapshot (sessionId: SessionId) =
        match OrchestratorProjection.tryFindByManagerSession sessionId snapshot.AgentProjections.Orchestrator with
        | None -> true
        | Some job ->
            match job.Progress with
            | JobProgress.ManagerStarted
            | JobProgress.ConflictPending _ -> true
            | _ -> false

    let missingTree (journal: AgentJournal option) (gitTreePort: GitTreePort option) sessionId =
        match journal, gitTreePort with
        | None, _ -> ReviewGuardUnavailable "Review guard requires an AgentJournal"
        | _, None -> ReviewGuardUnavailable "Review guard requires a GitTreePort"
        | Some journal, Some port ->
            try
                let sessionId = SessionId.create sessionId
                let snapshot = AgentJournal.snapshot journal

                if not (managerWorkRequiresGuard snapshot sessionId) then
                    ReviewGuardNotRequired
                else
                    let treeHash = port.GetTreeHash()

                    let emptyTree = "4b825dc642cb6eb9a060e54bf8d69288fbee4904"
                    let treeHash = treeHash.Trim()

                    let isEmpty =
                        String.IsNullOrWhiteSpace treeHash
                        || treeHash.Equals("NO_HEAD_TREE", StringComparison.Ordinal)
                        || treeHash.Equals(emptyTree, StringComparison.Ordinal)

                    let guard =
                        Map.tryFind sessionId snapshot.AgentProjections.Sessions
                        |> Option.bind (fun session -> session.ReviewGuard)

                    if isEmpty then
                        ReviewGuardMissing treeHash
                    else
                        match guard with
                        | Some state when ReviewProjection.satisfiesGuard (GitTreeHash.create treeHash) state ->
                            ReviewGuardConfirmed
                        | _ -> ReviewGuardMissing treeHash
            with ex ->
                ReviewGuardUnavailable(sprintf "Review guard dependency failed: %s" ex.Message)

    /// REVIEW-007: is a guard continuation for this session already outstanding.
    ///
    /// Derived from PROMPT-005 `PendingClaims`, which is the durable record of a
    /// continuation that was claimed and has not yet resolved. Deduplication must
    /// read that record rather than a guard-specific acceptance flag: "a plugin
    /// prompt landed" is a fact PROMPT-005 already owns, and a second name for it
    /// can disagree with the first.
    let private hasOutstandingGuardClaim
        (journal: AgentJournal)
        (targetSessionId: SessionId)
        (kind: PromptAuthority.ContinuationKind)
        =
        AgentProjection.tryFind targetSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.map (fun authority ->
            authority.PendingClaims
            |> Map.exists (fun _ claim -> claim.Origin = PromptAuthority.PromptOrigin.Continuation kind))
        |> Option.defaultValue false

    // OpenCode creates one plugin instance for the root and one for every manager
    // worktree. This reservation is process-wide so two instances reconciling the
    // same guard requirement cannot both pass the durable preflight before either claim lands.
    let private processNudgeKeys = HashSet<string>()

    let private guardNudgeKey
        (runtimeId: RuntimeId)
        (targetSessionId: SessionId)
        (dedupeOccasion: string)
        (reason: string)
        =
        sprintf
            "review-guard:%s:%s:%s:%s"
            (RuntimeId.value runtimeId)
            (SessionId.value targetSessionId)
            dedupeOccasion
            reason

    let private sessionDirectory (sessionId: SessionId) =
        match SharedState.SessionDirectories.TryGetValue(SessionId.value sessionId) with
        | true, dir -> Some dir
        | false, _ -> None

    let private sendGuardNudge
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        (targetSessionId: SessionId)
        (dedupeOccasion: string)
        (reason: string)
        (prompt: string)
        (agent: string)
        : Task<GuardNudgeOutcome> =
        task {
            match journal with
            | None -> return GuardNudgeOutcome.Failed "Review guard nudge requires an AgentJournal"
            | Some durable ->
                let nudgeKey =
                    guardNudgeKey (AgentJournal.runtimeId durable) targetSessionId dedupeOccasion reason

                let continuationKind =
                    match agent, reason with
                    | "reviewer", r when r.Contains("confirm-perfect") ->
                        PromptAuthority.ContinuationKind.ReviewConfirmation
                    | "reviewer", _ -> PromptAuthority.ContinuationKind.ReviewerGuard
                    | _ -> PromptAuthority.ContinuationKind.ManagerGuard

                // The synchronous check+reservation is the atomic point. A rejected
                // send releases it; an admitted/unknown send keeps it, because PROMPT-011
                // forbids licensing a duplicate while physical acceptance is unresolved.
                let reserved =
                    lock processNudgeKeys (fun () ->
                        if
                            hasOutstandingGuardClaim durable targetSessionId continuationKind
                            || nudgeKeys.Contains nudgeKey
                            || processNudgeKeys.Contains nudgeKey
                        then
                            false
                        else
                            nudgeKeys.Add nudgeKey |> ignore
                            processNudgeKeys.Add nudgeKey |> ignore
                            true)

                if not reserved then
                    return GuardNudgeOutcome.AlreadyOutstanding
                else
                    // PROMPT-006: Model=None. HostSessionNudge resolves Agent from the
                    // Authority Root's fallback cursor, so it is not passed here.
                    let! sent =
                        HostSessionNudge.sendContinuation
                            sessionPort
                            targetSessionId
                            prompt
                            continuationKind
                            (sessionDirectory targetSessionId)
                            (Some durable)
                            None

                    match sent with
                    | Ok key -> return GuardNudgeOutcome.Sent key
                    | Error error ->
                        lock processNudgeKeys (fun () ->
                            nudgeKeys.Remove nudgeKey |> ignore
                            processNudgeKeys.Remove nudgeKey |> ignore)

                        return GuardNudgeOutcome.Failed error
        }

    let nudgeManager
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        (sessionId: SessionId)
        (treeHash: string)
        =
        match journal with
        | Some durable when not (managerWorkRequiresGuard (AgentJournal.snapshot durable) sessionId) ->
            Task.FromResult GuardNudgeOutcome.NoLongerRequired
        | _ ->
            sendGuardNudge
                sessionPort
                journal
                nudgeKeys
                sessionId
                treeHash
                (sprintf "missing-review:%s" treeHash)
                RuntimeNudge.managerReviewGuard
                "manager"

    let nudgeReviewer
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        (sessionId: SessionId)
        (triggerProviderRun: ProviderRunIdentity)
        =
        sendGuardNudge
            sessionPort
            journal
            nudgeKeys
            sessionId
            (ProviderRunIdentity.value triggerProviderRun)
            "missing-verdict"
            RuntimeNudge.reviewerVerdictGuard
            "reviewer"

    /// REVIEW-003: the first PERFECT is recorded but not confirmed. This nudge only
    /// makes the Host start the next provider request; the confirmation itself comes
    /// from the second run's input seal proving it consumed the challenge.
    ///
    /// The prompt text IS `ReviewChallenge.Prompt`. It is not spelled again here
    /// because the digest of that exact TOML string is what the seal is searched for —
    /// a second copy that drifted by one character would fail every confirmation
    /// while looking like correct fail-closed behaviour.
    let requestPerfectConfirmation
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        (sessionId: SessionId)
        (triggerProviderRun: ProviderRunIdentity)
        =
        sendGuardNudge
            sessionPort
            journal
            nudgeKeys
            sessionId
            (ProviderRunIdentity.value triggerProviderRun)
            "confirm-perfect"
            ReviewChallenge.Prompt
            "reviewer"
