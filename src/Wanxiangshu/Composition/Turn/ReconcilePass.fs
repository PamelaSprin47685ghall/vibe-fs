namespace Wanxiangshu.Composition.Turn

open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Single reconcile pass: snapshot → evidence → reread/publish until causal decision.
/// Owns no queue / generation / single-flight state — Scheduler supplies those ports.
module ReconcilePass =

    [<Emit("console.error($0, $1)")>]
    let private logError (prefix: string) (message: string) : unit = jsNative

    let private publishTurnOf (turn: ReconciledTurn) : ReconcileProgram.PublishTurn =
        { SessionId = turn.SessionId
          PhysicalUserMessageId = turn.PhysicalUserMessageId
          ProviderRun = turn.ProviderRun
          Outcome = turn.Outcome }

    let private evidenceOf (turn: ReconciledTurn option) : ReconcileProgram.ReconcileEvidence =
        match turn with
        | None -> ReconcileProgram.ReconcileEvidence.NoTurn
        | Some value ->
            match value.Observation with
            | Some ReconcileProgram.TurnUnknown ->
                // HOST-004 / rabbit §7: finish=None is SnapshotObservation evidence
                // (Unknown), wake-gated in decideStep. PublishTurn carries the
                // placeholder Outcome only — never TurnUnknown — so publishDecision
                // can seal/dedupe the handoff to TurnWorkflow / InteractionRepair.
                ReconcileProgram.ReconcileEvidence.Unknown(Some(ReconcileProgram.observedTurn (publishTurnOf value)))
            | None ->
                let observed = ReconcileProgram.observedTurn (publishTurnOf value)

                match value.Outcome with
                | ReconcileProgram.TurnCompleted
                | ReconcileProgram.TurnAborted _
                | ReconcileProgram.TurnFailed _ -> ReconcileProgram.ReconcileEvidence.Terminal observed
                | ReconcileProgram.TurnInProgress
                | ReconcileProgram.TurnNeedsContinuation _ -> ReconcileProgram.ReconcileEvidence.Provisional observed

    let private publishIfAllowed
        (isCurrent: SessionId -> int -> bool)
        (recordMaps: SessionId -> ReconcileProgram.PublishMaps -> unit)
        (observeSnapshot: SessionId -> SessionMessage list -> Task)
        (onTurn: ReconciledTurnContext -> Task)
        (sessionId: SessionId)
        (generation: int)
        (wake: ReconcileProgram.ReconcileWake)
        (maps: ReconcileProgram.PublishMaps)
        (turn: ReconcileProgram.PublishTurn option)
        (turns: Map<string, ReconciledTurn>)
        (lastSnapshot: SessionMessage list option)
        : Task =
        task {
            match turn with
            | None ->
                match lastSnapshot with
                | Some messages when isCurrent sessionId generation -> do! observeSnapshot sessionId messages
                | _ -> ()
            | Some value ->
                let decision = ReconcileProgram.publishDecision maps value

                let delivery =
                    if decision.shouldPublish then
                        Some ReconciledTurnDelivery.Observation
                    else
                        match wake with
                        | ReconcileProgram.ReconcileWake.IdleWake _ -> Some ReconciledTurnDelivery.IdleRevisit
                        | ReconcileProgram.ReconcileWake.RetryWake
                        | ReconcileProgram.ReconcileWake.FailureWake
                        | ReconcileProgram.ReconcileWake.AbortWake -> None

                match delivery with
                | Some delivery when isCurrent sessionId generation ->
                    match Map.tryFind (ReconcileProgram.consumeKey value) turns with
                    | Some reconciled ->
                        let quiescence =
                            match wake with
                            | ReconcileProgram.ReconcileWake.IdleWake permit -> Some permit
                            | ReconcileProgram.ReconcileWake.RetryWake
                            | ReconcileProgram.ReconcileWake.FailureWake
                            | ReconcileProgram.ReconcileWake.AbortWake -> None

                        do!
                            onTurn
                                { Turn = reconciled
                                  Quiescence = quiescence
                                  Delivery = delivery }

                        if decision.shouldPublish && isCurrent sessionId generation then
                            recordMaps sessionId decision.maps
                    | None -> logError "RECONCILE-PUBLISH" "publish token missing from observed turns"
                | _ -> ()

                if isCurrent sessionId generation then
                    match lastSnapshot with
                    | Some messages -> do! observeSnapshot sessionId messages
                    | None -> ()
        }

    let rec private materializeActive
        (snapshot: ISessionSnapshotPort)
        (isCurrent: SessionId -> int -> bool)
        (isCleared: SessionId -> bool)
        (recordMaps: SessionId -> ReconcileProgram.PublishMaps -> unit)
        (observeSnapshot: SessionId -> SessionMessage list -> Task)
        (onTurn: ReconciledTurnContext -> Task)
        (maxErrors: int)
        (sessionId: SessionId)
        (generation: int)
        (wake: ReconcileProgram.ReconcileWake)
        (rereadsRemaining: int)
        (consecutiveErrors: int)
        (candidate: ReconcileProgram.PublishTurn option)
        (maps: ReconcileProgram.PublishMaps)
        (activeBinding: ActiveRunBinding)
        (turns: Map<string, ReconciledTurn>)
        (lastSnapshot: SessionMessage list option)
        : Task =
        task {
            if not (isCurrent sessionId generation) then
                return ()
            elif isCleared sessionId || not (isCurrent sessionId generation) then
                return ()
            else
                let! result = snapshot.GetMessages sessionId

                if not (isCurrent sessionId generation) then
                    return ()
                else
                    match result with
                    | Error error ->
                        logError "RECONCILE-SNAPSHOT" (sprintf "snapshot failed: %s" (string error))
                        let nextErrors = consecutiveErrors + 1

                        if nextErrors >= maxErrors then
                            // StopPass: keep Dirty for next host signal; errors do not consume causal budget.
                            if isCurrent sessionId generation then
                                match lastSnapshot with
                                | Some messages -> do! observeSnapshot sessionId messages
                                | None -> ()
                            else
                                return ()
                        elif isCurrent sessionId generation then
                            return!
                                materializeActive
                                    snapshot
                                    isCurrent
                                    isCleared
                                    recordMaps
                                    observeSnapshot
                                    onTurn
                                    maxErrors
                                    sessionId
                                    generation
                                    wake
                                    rereadsRemaining
                                    nextErrors
                                    candidate
                                    maps
                                    activeBinding
                                    turns
                                    lastSnapshot
                        else
                            return ()
                    | Ok messages ->
                        let turn = TurnReconcile.reconcile messages activeBinding
                        let evidence = evidenceOf turn

                        let observedTurns =
                            match turn with
                            | Some value -> Map.add (ReconcileProgram.consumeKey (publishTurnOf value)) value turns
                            | None -> turns

                        let decision = ReconcileProgram.decideStep wake rereadsRemaining evidence

                        match decision with
                        | ReconcileProgram.ReconcileDecision.Publish ->
                            // Stable observation handoff only (rabbit §7).
                            // Unknown under IdleWake Publishes the observed turn
                            // as-is; TurnWorkflow / InteractionRepair owns any
                            // missing-final-report repair (GLORY-070), gated on
                            // the pass's quiescence evidence.
                            let publishable =
                                match evidence with
                                | ReconcileProgram.ReconcileEvidence.Terminal observed -> observed.PublishTurn
                                | ReconcileProgram.ReconcileEvidence.Unknown(Some observed) -> observed.PublishTurn
                                | ReconcileProgram.ReconcileEvidence.Provisional observed -> observed.PublishTurn
                                | _ -> candidate

                            do!
                                publishIfAllowed
                                    isCurrent
                                    recordMaps
                                    observeSnapshot
                                    onTurn
                                    sessionId
                                    generation
                                    wake
                                    maps
                                    publishable
                                    observedTurns
                                    (Some messages)
                        | ReconcileProgram.ReconcileDecision.StopPass ->
                            if isCurrent sessionId generation then
                                do! observeSnapshot sessionId messages
                        | ReconcileProgram.ReconcileDecision.Reread(clearCandidate, remaining) ->
                            let candidate' =
                                if clearCandidate then
                                    None
                                else
                                    match evidence with
                                    | ReconcileProgram.ReconcileEvidence.Provisional observed -> observed.PublishTurn
                                    | _ -> candidate

                            if isCurrent sessionId generation then
                                return!
                                    materializeActive
                                        snapshot
                                        isCurrent
                                        isCleared
                                        recordMaps
                                        observeSnapshot
                                        onTurn
                                        maxErrors
                                        sessionId
                                        generation
                                        wake
                                        remaining
                                        0
                                        candidate'
                                        maps
                                        activeBinding
                                        observedTurns
                                        (Some messages)
                            else
                                return ()
        }

    /// Run one reconcile pass for a session generation.
    /// HOST-006: no active run still observes snapshot.
    let run
        (snapshot: ISessionSnapshotPort)
        (isCurrent: SessionId -> int -> bool)
        (isCleared: SessionId -> bool)
        (mapsFor: SessionId -> ReconcileProgram.PublishMaps)
        (recordMaps: SessionId -> ReconcileProgram.PublishMaps -> unit)
        (wake: ReconcileProgram.ReconcileWake)
        (observeSnapshot: SessionId -> SessionMessage list -> Task)
        (onTurn: ReconciledTurnContext -> Task)
        (maxRereads: int)
        (maxErrors: int)
        (activeBinding: ActiveRunBinding option)
        (sessionId: SessionId)
        (generation: int)
        : Task =
        task {
            if not (isCurrent sessionId generation) || isCleared sessionId then
                return ()
            else
                match activeBinding with
                | None ->
                    // HOST-006: still read + observe when no active run.
                    let! result = snapshot.GetMessages sessionId

                    if isCurrent sessionId generation then
                        match result with
                        | Ok messages -> do! observeSnapshot sessionId messages
                        | Error error -> logError "RECONCILE-SNAPSHOT" (sprintf "snapshot failed: %s" (string error))
                | Some bound ->
                    return!
                        materializeActive
                            snapshot
                            isCurrent
                            isCleared
                            recordMaps
                            observeSnapshot
                            onTurn
                            maxErrors
                            sessionId
                            generation
                            wake
                            (maxRereads + 1)
                            0
                            None
                            (mapsFor sessionId)
                            bound
                            Map.empty
                            None
        }
