namespace Wanxiangshu.Domain

open System
open Wanxiangshu.Kernel.Identity

/// M3 pure Reconcile Domain: Evidence → Decision → Program AST + Trace.
/// Zero task, zero mutable, zero I/O (FLOW-002 / FLOW-008).
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

    /// A snapshot can be classified without a publishable turn in trace-only
    /// tests. Production classification always supplies `Some PublishTurn`.
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
        | BudgetExhausted of hasCandidate: bool
        | SessionCleared

    [<RequireQualifiedAccess>]
    type ReconcileDecision =
        | RereadWithBackoff of clearContinuationCandidate: bool
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

    let pickDelay (sequence: int array) (index: int) (budgetRemaining: int) : int =
        if isNull sequence || sequence.Length = 0 || budgetRemaining <= 0 then
            0
        else
            let raw = sequence.[min index (sequence.Length - 1)]
            min raw budgetRemaining

    /// Ok snapshot resets backoff to 0; Error escalates (previous + 1).
    let nextBackoffIndex (previous: int) (snapshotOk: bool) : int = if snapshotOk then 0 else previous + 1

    let decideStep (evidence: ReconcileEvidence) : ReconcileDecision =
        match evidence with
        | ReconcileEvidence.SnapshotError _
        | ReconcileEvidence.NoTurn
        | ReconcileEvidence.Provisional _ -> ReconcileDecision.RereadWithBackoff false
        | ReconcileEvidence.Unknown _ -> ReconcileDecision.RereadWithBackoff true
        | ReconcileEvidence.Terminal _ -> ReconcileDecision.Publish
        | ReconcileEvidence.BudgetExhausted hasCandidate ->
            if hasCandidate then
                ReconcileDecision.Publish
            else
                ReconcileDecision.StopPass
        | ReconcileEvidence.SessionCleared -> ReconcileDecision.StopPass

    let decisionName (decision: ReconcileDecision) : string =
        match decision with
        | ReconcileDecision.RereadWithBackoff _ -> "RereadWithBackoff"
        | ReconcileDecision.Publish -> "Publish"
        | ReconcileDecision.StopPass -> "StopPass"

    let clearsContinuationCandidate (decision: ReconcileDecision) : bool =
        match decision with
        | ReconcileDecision.RereadWithBackoff clear -> clear
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

    // ── evidence / reply constructors (facade + tests) ───────────────────────

    let evidenceSnapshotError (reason: string) = ReconcileEvidence.SnapshotError reason
    let evidenceNoTurn () = ReconcileEvidence.NoTurn

    let observedTurn (turn: PublishTurn) : ObservedTurn =
        { Outcome = turn.Outcome
          PublishTurn = Some turn }

    let private traceObservation (outcome: TurnOutcome) : ObservedTurn =
        { Outcome = outcome
          PublishTurn = None }

    let evidenceProvisional (outcome: TurnOutcome) =
        ReconcileEvidence.Provisional(traceObservation outcome)

    let evidenceUnknown () = ReconcileEvidence.Unknown None

    let evidenceTerminal (outcome: TurnOutcome) =
        ReconcileEvidence.Terminal(traceObservation outcome)

    let evidenceObservedTerminal (turn: PublishTurn) =
        ReconcileEvidence.Terminal(observedTurn turn)

    let evidenceBudgetExhausted (hasCandidate: bool) =
        ReconcileEvidence.BudgetExhausted hasCandidate

    let evidenceSessionCleared () = ReconcileEvidence.SessionCleared

    // ── Program AST ──────────────────────────────────────────────────────────

    [<RequireQualifiedAccess>]
    type ReconcileCommand =
        | ReadActiveBinding of SessionId
        | ReadSnapshot of SessionId
        | Delay of delayMs: int
        | StorePublishMaps of SessionId * PublishMaps
        | PublishTurn of PublishTurn
        | ObserveSnapshot of SessionId
        | ProtocolMismatch of expected: string * actual: string

    [<RequireQualifiedAccess>]
    type ReconcileReply =
        | BindingPresent
        | BindingAbsent
        | SnapshotOk of ReconcileEvidence
        | SnapshotError of reason: string
        | DelayDone
        | PublishMapsStored
        | PublishDone
        | ObserveDone
        | UnitOk

    type ReconcileProgram =
        | Return of unit
        | Step of ReconcileCommand * (ReconcileReply -> ReconcileProgram)

    let replyBindingPresent () = ReconcileReply.BindingPresent
    let replyBindingAbsent () = ReconcileReply.BindingAbsent
    let replySnapshotOk (evidence: ReconcileEvidence) = ReconcileReply.SnapshotOk evidence
    let replySnapshotError (reason: string) = ReconcileReply.SnapshotError reason
    let replyDelayDone () = ReconcileReply.DelayDone
    let replyPublishMapsStored () = ReconcileReply.PublishMapsStored
    let replyPublishDone () = ReconcileReply.PublishDone
    let replyObserveDone () = ReconcileReply.ObserveDone
    let replyUnitOk () = ReconcileReply.UnitOk

    let private replyName (reply: ReconcileReply) : string =
        match reply with
        | ReconcileReply.BindingPresent -> "BindingPresent"
        | ReconcileReply.BindingAbsent -> "BindingAbsent"
        | ReconcileReply.SnapshotOk _ -> "SnapshotOk"
        | ReconcileReply.SnapshotError _ -> "SnapshotError"
        | ReconcileReply.DelayDone -> "DelayDone"
        | ReconcileReply.PublishMapsStored -> "PublishMapsStored"
        | ReconcileReply.PublishDone -> "PublishDone"
        | ReconcileReply.ObserveDone -> "ObserveDone"
        | ReconcileReply.UnitOk -> "UnitOk"

    let private protocolMismatch (expected: string) (reply: ReconcileReply) : ReconcileProgram =
        Step(
            ReconcileCommand.ProtocolMismatch(expected, replyName reply),
            function
            | ReconcileReply.UnitOk -> Return()
            | ReconcileReply.BindingPresent -> Return()
            | ReconcileReply.BindingAbsent -> Return()
            | ReconcileReply.SnapshotOk _ -> Return()
            | ReconcileReply.SnapshotError _ -> Return()
            | ReconcileReply.DelayDone -> Return()
            | ReconcileReply.PublishMapsStored -> Return()
            | ReconcileReply.PublishDone -> Return()
            | ReconcileReply.ObserveDone -> Return()
        )

    let private observeThenEnd (sessionId: SessionId) : ReconcileProgram =
        Step(
            ReconcileCommand.ObserveSnapshot sessionId,
            function
            | ReconcileReply.ObserveDone -> Return()
            | ReconcileReply.BindingPresent -> protocolMismatch "ObserveDone" ReconcileReply.BindingPresent
            | ReconcileReply.BindingAbsent -> protocolMismatch "ObserveDone" ReconcileReply.BindingAbsent
            | ReconcileReply.SnapshotOk evidence -> protocolMismatch "ObserveDone" (ReconcileReply.SnapshotOk evidence)
            | ReconcileReply.SnapshotError reason ->
                protocolMismatch "ObserveDone" (ReconcileReply.SnapshotError reason)
            | ReconcileReply.DelayDone -> protocolMismatch "ObserveDone" ReconcileReply.DelayDone
            | ReconcileReply.PublishMapsStored -> protocolMismatch "ObserveDone" ReconcileReply.PublishMapsStored
            | ReconcileReply.PublishDone -> protocolMismatch "ObserveDone" ReconcileReply.PublishDone
            | ReconcileReply.UnitOk -> protocolMismatch "ObserveDone" ReconcileReply.UnitOk
        )

    let private sealThenObserve (sessionId: SessionId) (maps: PublishMaps) : ReconcileProgram =
        Step(
            ReconcileCommand.StorePublishMaps(sessionId, maps),
            function
            | ReconcileReply.PublishMapsStored -> observeThenEnd sessionId
            | ReconcileReply.BindingPresent -> protocolMismatch "PublishMapsStored" ReconcileReply.BindingPresent
            | ReconcileReply.BindingAbsent -> protocolMismatch "PublishMapsStored" ReconcileReply.BindingAbsent
            | ReconcileReply.SnapshotOk evidence ->
                protocolMismatch "PublishMapsStored" (ReconcileReply.SnapshotOk evidence)
            | ReconcileReply.SnapshotError reason ->
                protocolMismatch "PublishMapsStored" (ReconcileReply.SnapshotError reason)
            | ReconcileReply.DelayDone -> protocolMismatch "PublishMapsStored" ReconcileReply.DelayDone
            | ReconcileReply.PublishDone -> protocolMismatch "PublishMapsStored" ReconcileReply.PublishDone
            | ReconcileReply.ObserveDone -> protocolMismatch "PublishMapsStored" ReconcileReply.ObserveDone
            | ReconcileReply.UnitOk -> protocolMismatch "PublishMapsStored" ReconcileReply.UnitOk
        )

    let private publishThenSealThenObserve
        (sessionId: SessionId)
        (turn: PublishTurn)
        (maps: PublishMaps)
        : ReconcileProgram =
        Step(
            ReconcileCommand.PublishTurn turn,
            function
            | ReconcileReply.PublishDone -> sealThenObserve sessionId maps
            | ReconcileReply.BindingPresent -> protocolMismatch "PublishDone" ReconcileReply.BindingPresent
            | ReconcileReply.BindingAbsent -> protocolMismatch "PublishDone" ReconcileReply.BindingAbsent
            | ReconcileReply.SnapshotOk evidence -> protocolMismatch "PublishDone" (ReconcileReply.SnapshotOk evidence)
            | ReconcileReply.SnapshotError reason ->
                protocolMismatch "PublishDone" (ReconcileReply.SnapshotError reason)
            | ReconcileReply.DelayDone -> protocolMismatch "PublishDone" ReconcileReply.DelayDone
            | ReconcileReply.PublishMapsStored -> protocolMismatch "PublishDone" ReconcileReply.PublishMapsStored
            | ReconcileReply.ObserveDone -> protocolMismatch "PublishDone" ReconcileReply.ObserveDone
            | ReconcileReply.UnitOk -> protocolMismatch "PublishDone" ReconcileReply.UnitOk
        )

    let private publishIfAllowed
        (sessionId: SessionId)
        (maps: PublishMaps)
        (turn: PublishTurn option)
        : ReconcileProgram =
        match turn with
        | None -> observeThenEnd sessionId
        | Some value ->
            let decision = publishDecision maps value

            if decision.shouldPublish then
                publishThenSealThenObserve sessionId value decision.maps
            else
                observeThenEnd sessionId

    /// One active-run materialization pass as data (FLOW-002).
    /// Interpreter supplies binding/snapshot/delay/publish/observe effects.
    let rec private materializeActive
        (sessionId: SessionId)
        (delays: int array)
        (budgetRemaining: int)
        (backoffIndex: int)
        (candidate: PublishTurn option)
        (maps: PublishMaps)
        : ReconcileProgram =
        if budgetRemaining <= 0 then
            publishIfAllowed sessionId maps candidate
        else
            Step(
                ReconcileCommand.ReadSnapshot sessionId,
                function
                | ReconcileReply.SnapshotError _ ->
                    let nextIdx = nextBackoffIndex backoffIndex false
                    let delayMs = pickDelay delays backoffIndex budgetRemaining

                    if delayMs <= 0 then
                        observeThenEnd sessionId
                    else
                        Step(
                            ReconcileCommand.Delay delayMs,
                            function
                            | ReconcileReply.DelayDone ->
                                materializeActive sessionId delays (budgetRemaining - delayMs) nextIdx candidate maps
                            | ReconcileReply.BindingPresent ->
                                protocolMismatch "DelayDone" ReconcileReply.BindingPresent
                            | ReconcileReply.BindingAbsent -> protocolMismatch "DelayDone" ReconcileReply.BindingAbsent
                            | ReconcileReply.SnapshotOk evidence ->
                                protocolMismatch "DelayDone" (ReconcileReply.SnapshotOk evidence)
                            | ReconcileReply.SnapshotError reason ->
                                protocolMismatch "DelayDone" (ReconcileReply.SnapshotError reason)
                            | ReconcileReply.PublishMapsStored ->
                                protocolMismatch "DelayDone" ReconcileReply.PublishMapsStored
                            | ReconcileReply.PublishDone -> protocolMismatch "DelayDone" ReconcileReply.PublishDone
                            | ReconcileReply.ObserveDone -> protocolMismatch "DelayDone" ReconcileReply.ObserveDone
                            | ReconcileReply.UnitOk -> protocolMismatch "DelayDone" ReconcileReply.UnitOk
                        )
                | ReconcileReply.SnapshotOk evidence ->
                    // Successful I/O resets escalation; classify then decide.
                    let afterOk = nextBackoffIndex backoffIndex true
                    let decision = decideStep evidence

                    match decision with
                    | ReconcileDecision.Publish ->
                        let turn =
                            match evidence with
                            | ReconcileEvidence.Terminal observed -> observed.PublishTurn
                            | ReconcileEvidence.BudgetExhausted _ -> candidate
                            | _ -> candidate

                        publishIfAllowed sessionId maps turn
                    | ReconcileDecision.StopPass -> observeThenEnd sessionId
                    | ReconcileDecision.RereadWithBackoff clearCandidate ->
                        let candidate' =
                            if clearCandidate then
                                None
                            else
                                match evidence with
                                | ReconcileEvidence.Provisional observed -> observed.PublishTurn
                                | _ -> candidate

                        // After Ok, backoff index is 0; production then increments after delay.
                        let delayMs = pickDelay delays afterOk budgetRemaining
                        let nextIdx = afterOk + 1

                        if delayMs <= 0 then
                            observeThenEnd sessionId
                        else
                            Step(
                                ReconcileCommand.Delay delayMs,
                                function
                                | ReconcileReply.DelayDone ->
                                    materializeActive
                                        sessionId
                                        delays
                                        (budgetRemaining - delayMs)
                                        nextIdx
                                        candidate'
                                        maps
                                | ReconcileReply.BindingPresent ->
                                    protocolMismatch "DelayDone" ReconcileReply.BindingPresent
                                | ReconcileReply.BindingAbsent ->
                                    protocolMismatch "DelayDone" ReconcileReply.BindingAbsent
                                | ReconcileReply.SnapshotOk evidence ->
                                    protocolMismatch "DelayDone" (ReconcileReply.SnapshotOk evidence)
                                | ReconcileReply.SnapshotError reason ->
                                    protocolMismatch "DelayDone" (ReconcileReply.SnapshotError reason)
                                | ReconcileReply.PublishMapsStored ->
                                    protocolMismatch "DelayDone" ReconcileReply.PublishMapsStored
                                | ReconcileReply.PublishDone -> protocolMismatch "DelayDone" ReconcileReply.PublishDone
                                | ReconcileReply.ObserveDone -> protocolMismatch "DelayDone" ReconcileReply.ObserveDone
                                | ReconcileReply.UnitOk -> protocolMismatch "DelayDone" ReconcileReply.UnitOk
                            )
                | ReconcileReply.BindingPresent ->
                    protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.BindingPresent
                | ReconcileReply.BindingAbsent ->
                    protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.BindingAbsent
                | ReconcileReply.DelayDone -> protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.DelayDone
                | ReconcileReply.PublishMapsStored ->
                    protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.PublishMapsStored
                | ReconcileReply.PublishDone -> protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.PublishDone
                | ReconcileReply.ObserveDone -> protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.ObserveDone
                | ReconcileReply.UnitOk -> protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.UnitOk
            )

    let materializePassWithMaps
        (session: string)
        (backoffDelaysMs: int array)
        (maxBudgetMs: int)
        (maps: PublishMaps)
        : ReconcileProgram =
        let sessionId = SessionId.create session
        let delays = if isNull backoffDelaysMs then [||] else backoffDelaysMs

        Step(
            ReconcileCommand.ReadActiveBinding sessionId,
            function
            | ReconcileReply.BindingAbsent ->
                // HOST-006: still read + observe when no active run.
                Step(
                    ReconcileCommand.ReadSnapshot sessionId,
                    function
                    | ReconcileReply.SnapshotOk _ -> observeThenEnd sessionId
                    | ReconcileReply.SnapshotError _ -> observeThenEnd sessionId
                    | ReconcileReply.BindingPresent ->
                        protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.BindingPresent
                    | ReconcileReply.BindingAbsent ->
                        protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.BindingAbsent
                    | ReconcileReply.DelayDone -> protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.DelayDone
                    | ReconcileReply.PublishMapsStored ->
                        protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.PublishMapsStored
                    | ReconcileReply.PublishDone ->
                        protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.PublishDone
                    | ReconcileReply.ObserveDone ->
                        protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.ObserveDone
                    | ReconcileReply.UnitOk -> protocolMismatch "SnapshotOk|SnapshotError" ReconcileReply.UnitOk
                )
            | ReconcileReply.BindingPresent -> materializeActive sessionId delays maxBudgetMs 0 None maps
            | ReconcileReply.SnapshotOk evidence ->
                protocolMismatch "BindingPresent|BindingAbsent" (ReconcileReply.SnapshotOk evidence)
            | ReconcileReply.SnapshotError reason ->
                protocolMismatch "BindingPresent|BindingAbsent" (ReconcileReply.SnapshotError reason)
            | ReconcileReply.DelayDone -> protocolMismatch "BindingPresent|BindingAbsent" ReconcileReply.DelayDone
            | ReconcileReply.PublishMapsStored ->
                protocolMismatch "BindingPresent|BindingAbsent" ReconcileReply.PublishMapsStored
            | ReconcileReply.PublishDone -> protocolMismatch "BindingPresent|BindingAbsent" ReconcileReply.PublishDone
            | ReconcileReply.ObserveDone -> protocolMismatch "BindingPresent|BindingAbsent" ReconcileReply.ObserveDone
            | ReconcileReply.UnitOk -> protocolMismatch "BindingPresent|BindingAbsent" ReconcileReply.UnitOk
        )

    let materializePass (session: string) (backoffDelaysMs: int array) (maxBudgetMs: int) : ReconcileProgram =
        materializePassWithMaps session backoffDelaysMs maxBudgetMs (publishMapsEmpty ())

    // ── Trace interpreter ────────────────────────────────────────────────────

    module TraceInterpreter =

        let commandName (command: ReconcileCommand) : string =
            match command with
            | ReconcileCommand.ReadActiveBinding _ -> "ReadActiveBinding"
            | ReconcileCommand.ReadSnapshot _ -> "ReadSnapshot"
            | ReconcileCommand.Delay _ -> "Delay"
            | ReconcileCommand.StorePublishMaps _ -> "StorePublishMaps"
            | ReconcileCommand.PublishTurn _ -> "PublishTurn"
            | ReconcileCommand.ObserveSnapshot _ -> "ObserveSnapshot"
            | ReconcileCommand.ProtocolMismatch _ -> "ProtocolMismatch"

        let stepName (program: ReconcileProgram) : string =
            match program with
            | Return _ -> "Return"
            | Step(command, _) -> commandName command

        let defaultReply (program: ReconcileProgram) : ReconcileReply =
            match program with
            | Step(ReconcileCommand.ReadActiveBinding _, _) -> ReconcileReply.BindingAbsent
            | Step(ReconcileCommand.ReadSnapshot _, _) -> ReconcileReply.SnapshotOk ReconcileEvidence.NoTurn
            | Step(ReconcileCommand.Delay _, _) -> ReconcileReply.DelayDone
            | Step(ReconcileCommand.StorePublishMaps _, _) -> ReconcileReply.PublishMapsStored
            | Step(ReconcileCommand.PublishTurn _, _) -> ReconcileReply.PublishDone
            | Step(ReconcileCommand.ObserveSnapshot _, _) -> ReconcileReply.ObserveDone
            | Step(ReconcileCommand.ProtocolMismatch _, _) -> ReconcileReply.UnitOk
            | Return _ -> ReconcileReply.UnitOk

        let rec interpretWith (replyOf: ReconcileProgram -> ReconcileReply) (program: ReconcileProgram) : string list =
            match program with
            | Return _ -> []
            | Step(command, next) as step -> commandName command :: interpretWith replyOf (next (replyOf step))

        let interpret (program: ReconcileProgram) : string list = interpretWith defaultReply program

    let stepName (program: ReconcileProgram) = TraceInterpreter.stepName program

    let interpretWith (replyOf: ReconcileProgram -> ReconcileReply) (program: ReconcileProgram) : string list =
        TraceInterpreter.interpretWith replyOf program

    let interpret (program: ReconcileProgram) : string list = TraceInterpreter.interpret program

/// Program builders (facade looks for ReconcilePrograms_materializePass).
module ReconcilePrograms =

    let materializePass (session: string) (backoffDelaysMs: int array) (maxBudgetMs: int) =
        ReconcileProgram.materializePass session backoffDelaysMs maxBudgetMs
