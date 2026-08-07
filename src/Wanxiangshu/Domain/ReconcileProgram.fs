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
    /// remaining > 1 且非终态 → Reread(remaining-1)；remaining <= 1 且非终态 → StopPass；Terminal → Publish。
    let decideStep (rereadsRemaining: int) (evidence: ReconcileEvidence) : ReconcileDecision =
        match evidence with
        | ReconcileEvidence.Terminal _ -> ReconcileDecision.Publish
        | ReconcileEvidence.SnapshotError _
        | ReconcileEvidence.NoTurn
        | ReconcileEvidence.Provisional _ ->
            if rereadsRemaining > 1 then
                ReconcileDecision.Reread(false, rereadsRemaining - 1)
            else
                ReconcileDecision.StopPass
        | ReconcileEvidence.Unknown _ ->
            if rereadsRemaining > 1 then
                ReconcileDecision.Reread(true, rereadsRemaining - 1)
            else
                ReconcileDecision.StopPass
        | ReconcileEvidence.SessionCleared -> ReconcileDecision.StopPass

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
