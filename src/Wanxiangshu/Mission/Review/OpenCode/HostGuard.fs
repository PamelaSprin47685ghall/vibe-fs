namespace Wanxiangshu.Mission.Review.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Resources
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

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

    /// Identity of one Reviewer guard continuation. Missing-verdict is scoped to
    /// the durable review requirement (barrier), never to whichever provider run
    /// happened to expose the missing judge. Confirmation is scoped to the first
    /// PERFECT provider run whose challenge must be consumed.
    [<RequireQualifiedAccess>]
    type private GuardNudgeOccasion =
        | MissingVerdict of ReviewBarrierId
        | PerfectConfirmation of ProviderRunIdentity

    let private kindForOccasion =
        function
        | GuardNudgeOccasion.MissingVerdict _ -> PromptAuthority.ContinuationKind.ReviewerGuard
        | GuardNudgeOccasion.PerfectConfirmation _ -> PromptAuthority.ContinuationKind.ReviewConfirmation

    let private occasionIdentity =
        function
        | GuardNudgeOccasion.MissingVerdict barrierId -> "barrier:" + ReviewBarrierId.value barrierId
        | GuardNudgeOccasion.PerfectConfirmation providerRun -> "perfect-run:" + ProviderRunIdentity.value providerRun

    /// REVIEW-007: is a guard continuation for this session already outstanding.
    ///
    /// Derived from PROMPT-005 `PendingClaims` on the *calling* instance journal.
    /// Cross-instance at-most-once is owned by SharedState.ReviewGuardNudges —
    /// journals do not share RuntimeId or PendingClaims across plugin instances.
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

    /// HOST-012: session-scoped reservation identity. Never RuntimeId and never
    /// the provider run that merely observed a missing verdict. Root/worktree
    /// instances can observe different provider runs for the same barrier; the
    /// barrier is the durable review requirement and therefore the idempotency key.
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
        =
        lock SharedState.ReviewGuardNudgeGate (fun () ->
            if
                hasOutstandingGuardClaim journal targetSessionId kind
                || SharedState.ReviewGuardNudges.Contains nudgeKey
            then
                false
            else
                SharedState.ReviewGuardNudges.Add nudgeKey |> ignore
                true)

    let private releaseKey (nudgeKey: string) =
        lock SharedState.ReviewGuardNudgeGate (fun () -> SharedState.ReviewGuardNudges.Remove nudgeKey |> ignore)

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
            | Some durable ->
                let continuationKind = kindForOccasion occasion
                let nudgeKey = guardNudgeKey targetSessionId occasion

                // Synchronous check+reservation is the atomic point. Rejected /
                // dead-worktree / Failed releases; admitted/unknown keeps it
                // (PROMPT-011: no second license while physical acceptance is unresolved).
                if not (tryReserve nudgeKey durable targetSessionId continuationKind) then
                    return GuardNudgeOutcome.AlreadyOutstanding
                else
                    let recordedDir = sessionDirectory targetSessionId

                    let worktreeIsAlive =
                        match recordedDir with
                        | None -> true
                        | Some dir -> Directory.Exists dir && File.Exists(Path.Combine(dir, "AGENTS.md"))

                    if not worktreeIsAlive then
                        releaseKey nudgeKey
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
                            releaseKey nudgeKey
                            return GuardNudgeOutcome.Failed error
        }

    let nudgeReviewer
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (sessionId: SessionId)
        : Task<GuardNudgeOutcome> =
        task {
            match journal with
            | None -> return GuardNudgeOutcome.Failed "Review guard nudge requires an AgentJournal"
            | Some durable ->
                match currentBarrier durable sessionId with
                | None -> return GuardNudgeOutcome.Failed "Review guard nudge requires an open review barrier"
                | Some barrierId ->
                    return!
                        sendGuardNudge
                            sessionPort
                            journal
                            sessionId
                            (GuardNudgeOccasion.MissingVerdict barrierId)
                            (ProviderProse.documentFor sessionId RuntimeNudge.ReviewerVerdictRequired Map.empty)
        }

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
        (sessionId: SessionId)
        (triggerProviderRun: ProviderRunIdentity)
        =
        sendGuardNudge
            sessionPort
            journal
            sessionId
            (GuardNudgeOccasion.PerfectConfirmation triggerProviderRun)
            (reviewChallengeVisibleBytes sessionId)

    /// Infrastructure adapter only: expose Host delivery/dedupe as the typed
    /// ReviewerContinuationPort consumed by Application ReviewerWorkflow.
    let continuationPort (sessionPort: ISessionHostPort) (journal: AgentJournal option) : ReviewerContinuationPort =
        { NudgeMissingVerdict =
            fun sessionId ->
                task {
                    let! _ = nudgeReviewer sessionPort journal sessionId
                    // Preserve existing boundary: missing-verdict send failure was
                    // not terminal; the next durable observation may re-enter.
                    return Ok()
                }
          SendPerfectChallenge =
            fun sessionId providerRun ->
                task {
                    match! requestPerfectConfirmation sessionPort journal sessionId providerRun with
                    | GuardNudgeOutcome.Failed reason -> return Error reason
                    | _ -> return Ok()
                } }
