namespace Wanxiangshu.Mission.Review.OpenCode

open System
open System.IO
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

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

    /// Identity of one Reviewer guard reminder. The barrier names the still-open
    /// business gate; ProviderRun names the exact terminal occasion. Duplicate
    /// observation of one terminal is idempotent, while a fresh terminal may
    /// remind again until the verdict gate closes.
    [<RequireQualifiedAccess>]
    type private GuardNudgeOccasion = MissingVerdict of ReviewBarrierId * ProviderRunIdentity

    let private kindForOccasion =
        function
        | GuardNudgeOccasion.MissingVerdict _ -> PromptAuthority.ContinuationKind.ReviewerGuard

    let private occasionIdentity =
        function
        | GuardNudgeOccasion.MissingVerdict(barrierId, providerRun) ->
            sprintf "barrier:%s:run:%s" (ReviewBarrierId.value barrierId) (ProviderRunIdentity.value providerRun)

    let private terminalOfOccasion =
        function
        | GuardNudgeOccasion.MissingVerdict(_, providerRun) -> providerRun

    let private gateKindOfOccasion =
        function
        | GuardNudgeOccasion.MissingVerdict(barrierId, _) ->
            RuntimeNudge.ReviewerVerdictRequired + ":" + ReviewBarrierId.value barrierId

    /// REVIEW-007: is a guard continuation for this session already outstanding.
    ///
    /// Derived from PROMPT-005 `PendingClaims` on the *calling* instance journal.
    /// Cross-instance at-most-once is owned by SharedState.ReviewGuardNudges —
    /// journals do not share RuntimeId or PendingClaims across plugin instances.
    let private hasOutstandingGuardClaim
        (journal: AgentJournal)
        (targetSessionId: SessionId)
        (kind: PromptAuthority.ContinuationKind)
        (occasion: GuardNudgeOccasion)
        =
        let payloadDigest =
            PromptAuthority.gateNudgePayloadDigest (gateKindOfOccasion occasion) (terminalOfOccasion occasion)

        AgentProjection.tryFind targetSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.map (fun authority ->
            authority.PendingClaims
            |> Map.exists (fun _ claim ->
                claim.Origin = PromptAuthority.PromptOrigin.Continuation kind
                && claim.PayloadDigest = payloadDigest))
        |> Option.defaultValue false

    /// HOST-012: exact-terminal reservation identity. Cross-instance duplicate
    /// observers of the same terminal collapse; a later terminal under the same
    /// still-open barrier is a fresh reminder occasion.
    let private guardNudgeKey (targetSessionId: SessionId) (occasion: GuardNudgeOccasion) =
        sprintf "review-guard:%s:%s" (SessionId.value targetSessionId) (occasionIdentity occasion)

    let private currentBarrier (journal: AgentJournal) (targetSessionId: SessionId) =
        AgentProjection.tryFind targetSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.ReviewGuard)
        |> Option.bind (fun guard -> guard.CurrentBarrierId)

    let private sessionDirectory (sessionId: SessionId) =
        match SharedState.SessionDirectories.TryGetValue(SessionId.value sessionId) with
        | true, dir -> Some dir
        | false, _ -> None

    let private tryReserve
        (nudgeKey: string)
        (journal: AgentJournal)
        (targetSessionId: SessionId)
        (kind: PromptAuthority.ContinuationKind)
        (occasion: GuardNudgeOccasion)
        =
        lock SharedState.ReviewGuardNudgeGate (fun () ->
            if
                hasOutstandingGuardClaim journal targetSessionId kind occasion
                || SharedState.ReviewGuardNudges.Contains nudgeKey
            then
                false
            else
                SharedState.ReviewGuardNudges.Add nudgeKey |> ignore
                true)

    let private releaseKey (nudgeKey: string) =
        lock SharedState.ReviewGuardNudgeGate (fun () -> SharedState.ReviewGuardNudges.Remove nudgeKey |> ignore)

    let private guardOutcomeFromGate nudgeKey =
        function
        | HostSessionNudge.GateContinuationOutcome.Sent key -> GuardNudgeOutcome.Sent key
        | HostSessionNudge.GateContinuationOutcome.AlreadyAdmitted -> GuardNudgeOutcome.AlreadyOutstanding
        | HostSessionNudge.GateContinuationOutcome.Retired ->
            releaseKey nudgeKey
            GuardNudgeOutcome.NoLongerRequired
        | HostSessionNudge.GateContinuationOutcome.Failed error ->
            releaseKey nudgeKey
            GuardNudgeOutcome.Failed error

    let private sendReservedGuardNudge
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (targetSessionId: SessionId)
        (continuationKind: PromptAuthority.ContinuationKind)
        (occasion: GuardNudgeOccasion)
        (nudgeKey: string)
        (prompt: string)
        =
        task {
            let recordedDir = sessionDirectory targetSessionId

            let worktreeIsAlive =
                recordedDir
                |> Option.map (fun dir -> Directory.Exists dir && File.Exists(Path.Combine(dir, "AGENTS.md")))
                |> Option.defaultValue true

            if not worktreeIsAlive then
                releaseKey nudgeKey
                return GuardNudgeOutcome.NoLongerRequired
            else
                let! sent =
                    HostSessionNudge.trySendGateContinuation
                        sessionPort
                        targetSessionId
                        prompt
                        continuationKind
                        recordedDir
                        (Some durable)
                        (gateKindOfOccasion occasion)
                        (terminalOfOccasion occasion)

                return guardOutcomeFromGate nudgeKey sent
        }

    let private sendGuardForDurable
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (targetSessionId: SessionId)
        (occasion: GuardNudgeOccasion)
        (prompt: string)
        =
        let continuationKind = kindForOccasion occasion
        let nudgeKey = guardNudgeKey targetSessionId occasion

        if not (tryReserve nudgeKey durable targetSessionId continuationKind occasion) then
            Task.FromResult GuardNudgeOutcome.AlreadyOutstanding
        else
            sendReservedGuardNudge sessionPort durable targetSessionId continuationKind occasion nudgeKey prompt

    let private sendGuardNudge
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (targetSessionId: SessionId)
        (occasion: GuardNudgeOccasion)
        (prompt: string)
        : Task<GuardNudgeOutcome> =
        task {
            match journal with
            | None -> return GuardNudgeOutcome.Failed "Review guard nudge requires an AgentJournal"
            | Some durable -> return! sendGuardForDurable sessionPort durable targetSessionId occasion prompt
        }

    let private nudgeDurableReviewer
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal)
        (sessionId: SessionId)
        (terminalProviderRun: ProviderRunIdentity)
        =
        task {
            match currentBarrier journal sessionId with
            | None -> return GuardNudgeOutcome.Failed "Review guard nudge requires an open review barrier"
            | Some barrierId ->
                return!
                    sendGuardNudge
                        sessionPort
                        (Some journal)
                        sessionId
                        (GuardNudgeOccasion.MissingVerdict(barrierId, terminalProviderRun))
                        (ProviderProse.documentFor sessionId RuntimeNudge.ReviewerVerdictRequired Map.empty)
        }

    let nudgeReviewer
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (terminalProviderRun: ProviderRunIdentity)
        : Task<GuardNudgeOutcome> =
        match journal with
        | None -> Task.FromResult(GuardNudgeOutcome.Failed "Review guard nudge requires an AgentJournal")
        | Some durable -> nudgeDurableReviewer sessionPort durable sessionId terminalProviderRun

    /// Infrastructure adapter only: expose Host delivery/dedupe as the typed
    /// ReviewerContinuationPort consumed by Application ReviewerWorkflow.
    let continuationPort (sessionPort: ISessionHostPort) (journal: AgentJournal option) : ReviewerContinuationPort =
        { NudgeMissingVerdict =
            fun sessionId terminalProviderRun ->
                task {
                    let! _ = nudgeReviewer sessionPort journal sessionId terminalProviderRun
                    // Preserve existing boundary: missing-verdict send failure was
                    // not terminal; the next durable observation may re-enter.
                    return Ok()
                } }
