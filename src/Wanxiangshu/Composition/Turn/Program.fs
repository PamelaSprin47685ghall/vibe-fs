namespace Wanxiangshu.Composition.Turn

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Reconcile pure Domain: Evidence → Decision + publish seals.
/// Zero task, zero mutable, zero I/O (FLOW-001 / FLOW-004).
/// Workflow CE lives in Composition/Turn/Scheduler.fs.
module ReconcileProgram =

    // ── outcomes (Domain-owned; Application maps wire into these) ─────────────

    type TurnOutcome =
        | TurnInProgress
        | TurnNeedsContinuation of reason: string
        | TurnCompleted
        | TurnAborted of reason: string
        | TurnFailed of error: string

    /// Reconciliation-private. Not a publishable TurnOutcome case (HOST-004).
    /// finish=None stable snapshot observation; must not cross the publish boundary.
    type SnapshotObservation = | TurnUnknown

    /// Stable production identity carried from snapshot classification to publish.
    type PublishTurn =
        { SessionId: SessionId
          PhysicalUserMessageId: PhysicalUserMessageId
          ProviderRun: ProviderRunIdentity
          Outcome: TurnOutcome }

    /// A snapshot can be classified without a publishable turn in pure tests.
    /// Production classification always supplies `Some PublishTurn`.
    type ObservedTurn =
        { Outcome: TurnOutcome
          PublishTurn: PublishTurn option }

    // ── evidence / decision ──────────────────────────────────────────────────

    /// What started this reconcile pass (HOST-004 rev.3).
    ///
    /// Only an `IdleWake` carries quiescence evidence — a fresh `SessionIdle`
    /// observation. Retry / failure wakes never grant idle-derived continuation
    /// rights. FailureWake freezes the physical user identity observed when the
    /// session.error arrived; this is failure finality, not ProviderRun identity.
    /// The scheduler generation (single-flight fencing) is a different physical
    /// concept and must never be reused as the attempt serial.
    [<RequireQualifiedAccess>]
    type ReconcileWake =
        | IdleWake of QuiescencePermit
        | RetryWake
        | FailureWake of physicalUserMessageId: PhysicalUserMessageId option * reason: string
        /// HOST-004: operator abort is a typed wake category, not a failure.
        /// It never carries idle/repair rights: under it, Unknown / Provisional
        /// must never Publish a stable observation that business could turn into
        /// InteractionRepair / "#".
        | AbortWake

    [<RequireQualifiedAccess>]
    type ReconcileEvidence =
        | SnapshotError of reason: string
        | NoTurn
        | Provisional of ObservedTurn
        | Unknown of ObservedTurn option
        | Terminal of ObservedTurn
        | SessionCleared

    /// Pure observation vocabulary (rabbit §7 / S3): Reconcile answers only
    /// whether an observation is stable enough to hand to business — never
    /// which repair prompt business should send.
    [<RequireQualifiedAccess>]
    type ReconcileDecision =
        | Reread of clearContinuationCandidate: bool * rereadsRemaining: int
        | Publish
        | StopPass

    // ── pure classifiers ─────────────────────────────────────────────────────

    let outcomeOf (name: string) : TurnOutcome =
        match name with
        | "TurnCompleted" -> TurnCompleted
        | "TurnAborted" -> TurnAborted "aborted"
        | "TurnFailed" -> TurnFailed "failed"
        | "TurnInProgress" -> TurnInProgress
        | "TurnNeedsContinuation" -> TurnNeedsContinuation "needs-continuation"
        | "TurnUnknown" -> invalidArg "name" "TurnUnknown is SnapshotObservation, not a TurnOutcome"
        | other -> TurnFailed(sprintf "unknown-outcome:%s" other)

    let isTerminalOutcome (outcome: TurnOutcome) : bool =
        match outcome with
        | TurnCompleted
        | TurnAborted _
        | TurnFailed _ -> true
        | TurnInProgress
        | TurnNeedsContinuation _ -> false

    let tryFailureWitnessReason (wake: ReconcileWake) (turn: PublishTurn) : string option =
        match wake with
        | ReconcileWake.FailureWake(Some physical, reason) when
            physical = turn.PhysicalUserMessageId && not (isTerminalOutcome turn.Outcome)
            ->
            Some reason
        | ReconcileWake.IdleWake _
        | ReconcileWake.RetryWake
        | ReconcileWake.FailureWake _
        | ReconcileWake.AbortWake -> None

    let private decideExhaustedRetryable (_rereadsRemaining: int) (exhausted: ReconcileDecision) =
        // HOST-BOUNDARY-005: no read is authorized by a counter. A later read
        // needs a new coarse Host signal or exact projection-change edge.
        exhausted

    let private exhaustedUnknownDecision (wake: ReconcileWake) =
        match wake with
        | ReconcileWake.IdleWake _ ->
            // Classic black hole: SessionIdle + assistant reasoning +
            // finish=None + rereads exhausted. Observation is stable —
            // Publish to business; never StopPass silently. Whether to
            // send missing-final-report is TurnWorkflow / InteractionRepair.
            ReconcileDecision.Publish
        | ReconcileWake.RetryWake
        | ReconcileWake.FailureWake _
        | ReconcileWake.AbortWake ->
            // No idle rights. Observation stability only, or an abort that
            // must not resurrect an idle-derived continuation; the next
            // real physical signal re-kicks, and a genuine TurnAborted
            // terminal publishes normally.
            ReconcileDecision.StopPass

    let private decideExhaustedUnknown (wake: ReconcileWake) (_rereadsRemaining: int) = exhaustedUnknownDecision wake

    let private decideExhaustedProvisional (wake: ReconcileWake) =
        match wake with
        | ReconcileWake.IdleWake _ ->
            // Idle is the only wake that proves the exact current physical
            // execution is quiescent. A provisional/unknown assistant under
            // that witness belongs to business repair.
            ReconcileDecision.Publish
        | ReconcileWake.RetryWake
        | ReconcileWake.FailureWake _
        | ReconcileWake.AbortWake ->
            // Retry/abort status can race the public session projection. A
            // FailureWake is handled earlier only when its frozen physical id
            // matches this exact snapshot assistant. All remaining non-idle
            // provisional observations stay parked for a causal edge.
            ReconcileDecision.StopPass

    let private tokenAlreadySealed (maps: Map<string, string>) (key: string) (token: string) =
        match Map.tryFind key maps with
        | Some previous when previous = token -> true
        | _ -> false

    /// Reconcile decision for one snapshot observation. Production Scheduler
    /// supplies a single read per causal edge (HOST-BOUNDARY-005); the remaining
    /// counter stays only as a pure compatibility surface for historical tests.
    ///
    /// 不变量（GLORY-070 / HOST-004 rev.3 / rabbit §7）：SessionIdle 被消费后只允许
    /// 产生一个稳定观测交接或明确 fail closed；因果重读耗尽绝不静默 StopPass 一个带
    /// idle evidence 的稳定 `Unknown`。`Unknown`（finish=None）耗尽后只有在 `IdleWake`
    /// 下 `Publish` 给业务（TurnWorkflow / InteractionRepair 决定是否 repair）；
    /// `Retry` wake 只证明 provider lifecycle edge；`FailureWake` 还携带 signal 到达时冻结的
    /// physical user identity。只有该 physical 与 snapshot exact current assistant 一致时，
    /// Unknown / Provisional 才被收敛成 TurnFailed；否则仍 StopPass 等待 causal edge。
    /// 两者都不授予 idle repair 权。普通 Unknown / Provisional 只有 IdleWake 可 Publish；
    /// `SnapshotError` / `NoTurn` 同样 StopPass。
    let decideStep (wake: ReconcileWake) (rereadsRemaining: int) (evidence: ReconcileEvidence) : ReconcileDecision =
        let hasFailureWitness =
            match evidence with
            | ReconcileEvidence.Provisional observed
            | ReconcileEvidence.Unknown(Some observed) ->
                observed.PublishTurn |> Option.bind (tryFailureWitnessReason wake) |> Option.isSome
            | ReconcileEvidence.SnapshotError _
            | ReconcileEvidence.NoTurn
            | ReconcileEvidence.Unknown None
            | ReconcileEvidence.Terminal _
            | ReconcileEvidence.SessionCleared -> false

        match hasFailureWitness, evidence with
        | true, (ReconcileEvidence.Provisional _ | ReconcileEvidence.Unknown _) -> ReconcileDecision.Publish
        | false, ReconcileEvidence.Terminal _ -> ReconcileDecision.Publish
        | false, (ReconcileEvidence.SnapshotError _ | ReconcileEvidence.NoTurn) ->
            decideExhaustedRetryable rereadsRemaining ReconcileDecision.StopPass
        | false, ReconcileEvidence.Provisional _ ->
            decideExhaustedRetryable rereadsRemaining (decideExhaustedProvisional wake)
        | false, ReconcileEvidence.Unknown _ -> decideExhaustedUnknown wake rereadsRemaining
        | false, ReconcileEvidence.SessionCleared -> ReconcileDecision.StopPass
        | true, _ -> ReconcileDecision.StopPass

    let decisionName (decision: ReconcileDecision) : string =
        match decision with
        | ReconcileDecision.Reread _ -> "Reread"
        | ReconcileDecision.Publish -> "Publish"
        | ReconcileDecision.StopPass -> "StopPass"

    let clearsContinuationCandidate (decision: ReconcileDecision) : bool =
        match decision with
        | ReconcileDecision.Reread(clear, _) -> clear
        | ReconcileDecision.Publish
        | ReconcileDecision.StopPass -> false

    let consumeKey (turn: PublishTurn) : string =
        String.Concat(
            [| SessionId.value turn.SessionId
               "|"
               PhysicalUserMessageId.value turn.PhysicalUserMessageId
               "|"
               ProviderRunIdentity.value turn.ProviderRun |]
        )

    // ── publish maps (consumed = terminal seal; provisional = incomplete seal) ─
    // Class so Fable emits instance methods callable from domain.mjs tests.

    type PublishMaps(consumed: Map<string, string>, provisional: Map<string, string>) =
        member _.Consumed = consumed
        member _.Provisional = provisional

        member _.provisionalHas(turn: PublishTurn) : bool =
            let key = SessionId.value turn.SessionId
            let token = consumeKey turn

            match Map.tryFind key provisional with
            | Some previous when previous = token -> true
            | _ -> false

        member _.consumedHas(turn: PublishTurn) : bool =
            let key = SessionId.value turn.SessionId
            let token = consumeKey turn

            match Map.tryFind key consumed with
            | Some previous when previous = token -> true
            | _ -> false

    let publishMapsEmpty () : PublishMaps = PublishMaps(Map.empty, Map.empty)

    let publishDecision
        (maps: PublishMaps)
        (turn: PublishTurn)
        : {| shouldPublish: bool
             maps: PublishMaps |}
        =
        let key = SessionId.value turn.SessionId
        let token = consumeKey turn

        // HOST-004: TurnUnknown is SnapshotObservation — type-unreachable here.
        // IdleWake → Publish (stable Unknown handoff) lives in decideStep; business
        // repair is TurnWorkflow / InteractionRepair, not this seal layer.
        if isTerminalOutcome turn.Outcome && tokenAlreadySealed maps.Consumed key token then
            {| shouldPublish = false; maps = maps |}
        elif isTerminalOutcome turn.Outcome then
            {| shouldPublish = true
               maps = PublishMaps(Map.add key token maps.Consumed, Map.remove key maps.Provisional) |}
        elif tokenAlreadySealed maps.Provisional key token then
            {| shouldPublish = false; maps = maps |}
        else
            {| shouldPublish = true
               maps = PublishMaps(maps.Consumed, Map.add key token maps.Provisional) |}

    let clearProvisional (maps: PublishMaps) (sessionKey: string) : PublishMaps =
        PublishMaps(maps.Consumed, Map.remove sessionKey maps.Provisional)

    let turnFixture (session: string) (physical: string) (providerRun: string) (outcome: TurnOutcome) : PublishTurn =
        { SessionId = SessionId.create session
          PhysicalUserMessageId = PhysicalUserMessageId.create physical
          ProviderRun = ProviderRunIdentity.create providerRun
          Outcome = outcome }

    // ── evidence constructors (facade + tests) ───────────────────────────────

    let evidenceSnapshotError (reason: string) = ReconcileEvidence.SnapshotError reason
    let evidenceNoTurn () = ReconcileEvidence.NoTurn

    let observedTurn (turn: PublishTurn) : ObservedTurn =
        { Outcome = turn.Outcome
          PublishTurn = Some turn }

    let private pureObservation (outcome: TurnOutcome) : ObservedTurn =
        { Outcome = outcome
          PublishTurn = None }

    let evidenceProvisional (outcome: TurnOutcome) =
        ReconcileEvidence.Provisional(pureObservation outcome)

    let evidenceUnknown () = ReconcileEvidence.Unknown None

    let evidenceTerminal (outcome: TurnOutcome) =
        ReconcileEvidence.Terminal(pureObservation outcome)

    let evidenceSessionCleared () = ReconcileEvidence.SessionCleared
