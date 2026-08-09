namespace Wanxiangshu.Domain

open System
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Reconcile pure Domain: Evidence → Decision + publish seals.
/// Zero task, zero mutable, zero I/O (FLOW-001 / FLOW-004).
/// Workflow CE lives in Application/Reconciliation/Reconciler.fs.
module ReconcileProgram =

    // ── outcomes (Domain-owned; Application maps wire into these) ─────────────

    type TurnOutcome =
        | TurnInProgress
        | TurnNeedsContinuation of reason: string
        | TurnCompleted
        | TurnAborted of reason: string
        | TurnFailed of error: string
        | TurnUnknown

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
    /// rights, and the scheduler generation (single-flight fencing) is a
    /// different physical concept and must never be reused as the attempt
    /// serial.
    [<RequireQualifiedAccess>]
    type ReconcileWake =
        | IdleWake of QuiescencePermit
        | RetryWake
        | FailureWake
        /// HOST-004: operator abort is a typed wake category, not a failure.
        /// It never carries idle/repair rights: under it, Unknown / Provisional
        /// must never produce RepairMissingFinalReport / InteractionRepair / "#".
        | AbortWake

    [<RequireQualifiedAccess>]
    type ReconcileEvidence =
        | SnapshotError of reason: string
        | NoTurn
        | Provisional of ObservedTurn
        | Unknown of ObservedTurn option
        | Terminal of ObservedTurn
        | SessionCleared

    [<RequireQualifiedAccess>]
    type ReconcileDecision =
        | Reread of clearContinuationCandidate: bool * rereadsRemaining: int
        | Publish
        /// GLORY-070 / HOST-004 rev.3: a stable idle that never settled into a
        /// terminal (finish=None) must produce the missing-final-report repair
        /// instead of a silent StopPass black hole — but only when the pass
        /// actually carries idle evidence. Without an `IdleWake`, `Unknown` is
        /// observation stability only, not quiescence: no idle-derived
        /// continuation may be constructed.
        | RepairMissingFinalReport
        | StopPass

    // ── pure classifiers ─────────────────────────────────────────────────────

    let outcomeOf (name: string) : TurnOutcome =
        match name with
        | "TurnCompleted" -> TurnCompleted
        | "TurnAborted" -> TurnAborted "aborted"
        | "TurnFailed" -> TurnFailed "failed"
        | "TurnInProgress" -> TurnInProgress
        | "TurnNeedsContinuation" -> TurnNeedsContinuation "needs-continuation"
        | "TurnUnknown" -> TurnUnknown
        | other -> TurnFailed(sprintf "unknown-outcome:%s" other)

    let isTerminalOutcome (outcome: TurnOutcome) : bool =
        match outcome with
        | TurnCompleted
        | TurnAborted _
        | TurnFailed _ -> true
        | TurnInProgress
        | TurnNeedsContinuation _
        | TurnUnknown -> false

    /// 有界因果重读：rereadsRemaining = 还能进行多少次读取判定（初始 = maxCausalRereads + 1）。
    ///
    /// 不变量（GLORY-070 / HOST-004 rev.3）：SessionIdle 被消费后只允许产生一个
    /// 稳定业务决定或明确 fail closed；因果重读耗尽绝不静默 StopPass 一个带 idle
    /// evidence 的稳定 `Unknown`。`Unknown`（finish=None）耗尽后只有在 `IdleWake`
    /// 下进入 `RepairMissingFinalReport`；`Retry` / `Failure` wake 只证明观测稳定、
    /// 不证明静止——不产生 idle-derived continuation（StopPass，等下一个 signal）。
    /// `Provisional`（finish=stop 无合法正文 / tool-calls 停滞）耗尽后发布；只有
    /// `SnapshotError` / `NoTurn` 保持 StopPass（没有任何可作用的对象，等待下一个
    /// Host signal 重踢）。
    let decideStep (wake: ReconcileWake) (rereadsRemaining: int) (evidence: ReconcileEvidence) : ReconcileDecision =
        match evidence with
        | ReconcileEvidence.Terminal _ -> ReconcileDecision.Publish
        | ReconcileEvidence.SnapshotError _
        | ReconcileEvidence.NoTurn ->
            if rereadsRemaining > 1 then
                ReconcileDecision.Reread(false, rereadsRemaining - 1)
            else
                ReconcileDecision.StopPass
        | ReconcileEvidence.Provisional _ ->
            if rereadsRemaining > 1 then
                ReconcileDecision.Reread(false, rereadsRemaining - 1)
            else
                match wake with
                | ReconcileWake.AbortWake ->
                    // HOST-004: under operator abort, stalled provisional must never
                    // publish an idle/repair continuation (InteractionRepair /
                    // "#"). StopPass and wait for the real TurnAborted terminal.
                    ReconcileDecision.StopPass
                | _ ->
                    // F1: exhausted TurnNeedsContinuation / stalled tool-call turn must
                    // publish so the repair branch runs instead of dying in StopPass.
                    ReconcileDecision.Publish
        | ReconcileEvidence.Unknown _ ->
            if rereadsRemaining > 1 then
                ReconcileDecision.Reread(true, rereadsRemaining - 1)
            else
                match wake with
                | ReconcileWake.IdleWake _ ->
                    // The classic black hole: SessionIdle + assistant reasoning +
                    // finish=None + rereads exhausted. Must auto-continue (missing
                    // final report) or fail closed — never StopPass silently.
                    ReconcileDecision.RepairMissingFinalReport
                | ReconcileWake.RetryWake
                | ReconcileWake.FailureWake
                | ReconcileWake.AbortWake ->
                    // NoIdle/Repair rights. Observation stability only, or an abort
                    // that must not resurrect an idle-derived continuation; the next
                    // real physical signal re-kicks, and a genuine TurnAborted
                    // terminal publishes normally.
                    ReconcileDecision.StopPass
        | ReconcileEvidence.SessionCleared -> ReconcileDecision.StopPass

    let decisionName (decision: ReconcileDecision) : string =
        match decision with
        | ReconcileDecision.Reread _ -> "Reread"
        | ReconcileDecision.Publish -> "Publish"
        | ReconcileDecision.RepairMissingFinalReport -> "RepairMissingFinalReport"
        | ReconcileDecision.StopPass -> "StopPass"

    let clearsContinuationCandidate (decision: ReconcileDecision) : bool =
        match decision with
        | ReconcileDecision.Reread(clear, _) -> clear
        | ReconcileDecision.Publish
        | ReconcileDecision.RepairMissingFinalReport
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

        match turn.Outcome with
        | TurnUnknown ->
            // HOST-004: TurnUnknown is a reconciliation observation only. It must
            // never cross the stable business-turn boundary — no terminal (stable)
            // and no incomplete (provisional) business-turn seal may be written.
            // The distinct IdleWake → RepairMissingFinalReport path lives in
            // decideStep and is untouched here.
            {| shouldPublish = false; maps = maps |}
        | _ ->
            if isTerminalOutcome turn.Outcome then
                match Map.tryFind key maps.Consumed with
                | Some previous when previous = token -> {| shouldPublish = false; maps = maps |}
                | _ ->
                    {| shouldPublish = true
                       maps = PublishMaps(Map.add key token maps.Consumed, Map.remove key maps.Provisional) |}
            else
                match Map.tryFind key maps.Provisional with
                | Some previous when previous = token -> {| shouldPublish = false; maps = maps |}
                | _ ->
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

    let evidenceObservedTerminal (turn: PublishTurn) =
        ReconcileEvidence.Terminal(observedTurn turn)

    let evidenceSessionCleared () = ReconcileEvidence.SessionCleared
