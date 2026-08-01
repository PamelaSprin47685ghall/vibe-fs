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
        | ReviewGuardUnavailable of reason: string

    /// REVIEW-003's shared barrier writer lives in `ReviewBarrier.openBarrier`
    /// (Journal layer, compiled before the fork paths). Both the Orchestrator's
    /// review barrier (ORCH-006) and a Manager's own guard-path review fork
    /// (REVIEW-007) emit the same fact through the same function.
    let openBarrier = ReviewBarrier.openBarrier

    let missingTree (journal: AgentJournal option) (gitTreePort: GitTreePort option) sessionId =
        match journal, gitTreePort with
        | None, _ -> ReviewGuardUnavailable "Review guard requires an AgentJournal"
        | _, None -> ReviewGuardUnavailable "Review guard requires a GitTreePort"
        | Some journal, Some port ->
            try
                let treeHash = port.GetTreeHash()

                let emptyTree = "4b825dc642cb6eb9a060e54bf8d69288fbee4904"
                let treeHash = treeHash.Trim()

                let isEmpty =
                    String.IsNullOrWhiteSpace treeHash
                    || treeHash.Equals("NO_HEAD_TREE", StringComparison.Ordinal)
                    || treeHash.Equals(emptyTree, StringComparison.Ordinal)

                if isEmpty then
                    ReviewGuardMissing treeHash
                else
                    let snapshot = AgentJournal.snapshot journal

                    let sessionOpt =
                        Map.tryFind (SessionId.create sessionId) snapshot.AgentProjections.Sessions

                    match sessionOpt with
                    | None -> ReviewGuardMissing treeHash
                    | Some session ->
                        match session.ReviewGuard with
                        | Some guard when guard.IsConfirmed && guard.LastGitTreeHash = Some(GitTreeHash.create treeHash) ->
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

    let private guardNudgeKey (targetSessionId: SessionId) (triggerProviderRun: ProviderRunIdentity) (reason: string) =
        sprintf
            "review-guard:%s:%s:%s"
            (SessionId.value targetSessionId)
            (ProviderRunIdentity.value triggerProviderRun)
            reason

    let private sendGuardNudge
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        (targetSessionId: SessionId)
        (triggerProviderRun: ProviderRunIdentity)
        (reason: string)
        (prompt: string)
        (agent: string)
        : Task<Result<PromptKey, string>> =
        task {
            match journal with
            | None -> return Error "Review guard nudge requires an AgentJournal"
            | Some durable ->
                let nudgeKey = guardNudgeKey targetSessionId triggerProviderRun reason

                let continuationKind =
                    match agent, reason with
                    | "reviewer", r when r.Contains("confirm-perfect") ->
                        PromptAuthority.ContinuationKind.ReviewConfirmation
                    | "reviewer", _ -> PromptAuthority.ContinuationKind.ReviewerGuard
                    | _ -> PromptAuthority.ContinuationKind.ManagerGuard

                // Dedupe before sending, never after. Recording the key on success
                // only is deliberate: a rejected send must stay retryable, because
                // acceptance is the thing being deduplicated and a failure is not one.
                if
                    hasOutstandingGuardClaim durable targetSessionId continuationKind
                    || nudgeKeys.Contains nudgeKey
                then
                    return Error(sprintf "Guard nudge already outstanding: %s" nudgeKey)
                else
                    // PROMPT-006: Model=None. HostSessionNudge resolves Agent from the
                    // Authority Root's fallback cursor, so it is not passed here.
                    let! sent =
                        HostSessionNudge.sendContinuation
                            sessionPort
                            targetSessionId
                            prompt
                            continuationKind
                            None
                            (Some durable)
                            None

                    // The claim is durable either way; the in-memory key only
                    // suppresses a second send within this process lifetime.
                    match sent with
                    | Ok _ -> nudgeKeys.Add nudgeKey |> ignore
                    | Error _ -> ()

                    return sent
        }

    let nudgeManager
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        (sessionId: SessionId)
        (triggerProviderRun: ProviderRunIdentity)
        (treeHash: string)
        =
        sendGuardNudge
            sessionPort
            journal
            nudgeKeys
            sessionId
            triggerProviderRun
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
            triggerProviderRun
            "missing-verdict"
            RuntimeNudge.reviewerVerdictGuard
            "reviewer"

    /// REVIEW-003: the first PERFECT is recorded but not confirmed. This nudge only
    /// makes the Host start the next provider request; the confirmation itself comes
    /// from the second run's input seal proving it consumed the challenge.
    ///
    /// The prompt text IS `ReviewChallenge.Text`. It is not spelled again here
    /// because the digest of that exact string is what the seal is searched for —
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
            triggerProviderRun
            "confirm-perfect"
            ReviewChallenge.Text
            "reviewer"
