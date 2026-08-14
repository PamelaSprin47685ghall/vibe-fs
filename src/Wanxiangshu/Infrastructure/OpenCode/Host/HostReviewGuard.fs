namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Review
open Wanxiangshu.Session

module HostReviewGuard =

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
                    | _ -> PromptAuthority.ContinuationKind.ReviewerGuard

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
                    let releaseKey () =
                        lock processNudgeKeys (fun () ->
                            nudgeKeys.Remove nudgeKey |> ignore
                            processNudgeKeys.Remove nudgeKey |> ignore)


                    // (a) Send-time directory guard. The recorded worktree may still
                    // exist while its AGENTS.md has already been unlinked, so a prompt
                    // built from it would drop the 102-byte instruction block. Only a
                    // *recorded* directory that is gone or missing AGENTS.md is a real
                    // seal-break condition: `None` (manager-forked children are never
                    // registered in SessionDirectories) is the normal root-fallback
                    // path and must keep sending.
                    let recordedDir = sessionDirectory targetSessionId

                    let worktreeIsAlive =
                        match recordedDir with
                        | None -> true
                        | Some dir -> Directory.Exists dir && File.Exists(Path.Combine(dir, "AGENTS.md"))

                    if not worktreeIsAlive then
                        releaseKey ()
                        return GuardNudgeOutcome.NoLongerRequired
                    else
                        // PROMPT-006: Model=None. HostSessionNudge resolves Agent from the
                        // Authority Root's fallback cursor, so it is not passed here.
                        let! sent =
                            HostSessionNudge.sendContinuation
                                sessionPort
                                targetSessionId
                                prompt
                                continuationKind
                                recordedDir
                                (Some durable)

                        match sent with
                        | Ok key -> return GuardNudgeOutcome.Sent key
                        | Error error ->
                            releaseKey ()
                            return GuardNudgeOutcome.Failed error
        }

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
            (ProviderProse.documentFor sessionId RuntimeNudge.ReviewerVerdictRequired Map.empty)
            "reviewer"

    /// REVIEW-003: the first PERFECT is recorded but not confirmed. This nudge only
    /// makes the Host start the next provider request; the confirmation itself comes
    /// from the second run's input seal proving it consumed the challenge.
    ///
    /// PROJ-008 Step5：可见字节经 `AppendReviewChallenge` → plan → render 归一，再取
    /// 尾部正文发送。生产路径与 algebra 必须字节一致；seal 搜索的仍是同一 Prompt digest。
    let private reviewChallengeVisibleBytes (sessionId: SessionId) : string =
        let prompt = ProviderProse.documentFor sessionId ReviewChallenge.Path Map.empty

        let emptyCurrent: ProviderProjection.ProviderSemanticProjection =
            { ProviderId = None
              ModelId = None
              Variant = None
              Tools = []
              System = []
              Messages = [] }

        let snapshot: ProjectionSnapshot =
            { CurrentProjection = emptyCurrent
              CommittedPrefix = None
              BlogFrames = []
              TransportMessages = Set.empty
              HostReanchor = None }

        let intents =
            [ ProjectionIntent.AppendReviewChallenge
                  { TextVersion = ReviewChallenge.TextVersion
                    Prompt = prompt } ]

        match ProjectionPlanner.plan intents with
        | Error _ -> prompt
        | Ok ordered ->
            let wire = ProjectionRenderer.renderMessagesWithIntents snapshot [] ordered

            wire
            |> List.tryLast
            |> Option.bind (fun msg ->
                msg.Parts
                |> List.tryPick (function
                    | ProviderProjection.WireText t -> Some t
                    | _ -> None))
            |> Option.defaultValue prompt

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
            (reviewChallengeVisibleBytes sessionId)
            "reviewer"

    /// Infrastructure adapter only: expose Host delivery/dedupe as the typed
    /// ReviewerContinuationPort consumed by Application ReviewerWorkflow.
    let continuationPort
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        : ReviewerContinuationPort =
        { NudgeMissingVerdict =
            fun sessionId providerRun ->
                task {
                    let! _ = nudgeReviewer sessionPort journal nudgeKeys sessionId providerRun
                    // Preserve existing boundary: missing-verdict send failure was
                    // not terminal; the next durable observation may re-enter.
                    return Ok()
                }
          SendPerfectChallenge =
            fun sessionId providerRun ->
                task {
                    match! requestPerfectConfirmation sessionPort journal nudgeKeys sessionId providerRun with
                    | GuardNudgeOutcome.Failed reason -> return Error reason
                    | _ -> return Ok()
                } }
