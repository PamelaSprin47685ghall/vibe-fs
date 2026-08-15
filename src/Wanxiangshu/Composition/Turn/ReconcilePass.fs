namespace Wanxiangshu.Composition.Turn

open System.Threading.Tasks
open Fable.Core
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
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.OpenCode

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

    let private classifyOutcome
        (outcome: ReconcileProgram.TurnOutcome)
        (observed: ReconcileProgram.ObservedTurn)
        : ReconcileProgram.ReconcileEvidence =
        match outcome with
        | ReconcileProgram.TurnCompleted
        | ReconcileProgram.TurnAborted _
        | ReconcileProgram.TurnFailed _ -> ReconcileProgram.ReconcileEvidence.Terminal observed
        | ReconcileProgram.TurnInProgress
        | ReconcileProgram.TurnNeedsContinuation _ -> ReconcileProgram.ReconcileEvidence.Provisional observed

    let private evidenceFromObserved (value: ReconciledTurn) : ReconcileProgram.ReconcileEvidence =
        match value.Observation with
        | Some ReconcileProgram.TurnUnknown ->
            // HOST-004 / rabbit §7: finish=None is SnapshotObservation evidence
            // (Unknown), wake-gated in decideStep. PublishTurn carries the
            // placeholder Outcome only — never TurnUnknown — so publishDecision
            // can seal/dedupe the handoff to TurnWorkflow / InteractionRepair.
            ReconcileProgram.ReconcileEvidence.Unknown(Some(ReconcileProgram.observedTurn (publishTurnOf value)))
        | None -> classifyOutcome value.Outcome (ReconcileProgram.observedTurn (publishTurnOf value))

    let private evidenceOf (turn: ReconciledTurn option) : ReconcileProgram.ReconcileEvidence =
        match turn with
        | None -> ReconcileProgram.ReconcileEvidence.NoTurn
        | Some value -> evidenceFromObserved value

    let private deliveryOf
        (shouldPublish: bool)
        (wake: ReconcileProgram.ReconcileWake)
        : ReconciledTurnDelivery option =
        match shouldPublish, wake with
        | true, _ -> Some ReconciledTurnDelivery.Observation
        | false, ReconcileProgram.ReconcileWake.IdleWake _ -> Some ReconciledTurnDelivery.IdleRevisit
        | false, ReconcileProgram.ReconcileWake.RetryWake
        | false, ReconcileProgram.ReconcileWake.FailureWake
        | false, ReconcileProgram.ReconcileWake.AbortWake -> None

    let private quiescenceOf (wake: ReconcileProgram.ReconcileWake) =
        match wake with
        | ReconcileProgram.ReconcileWake.IdleWake permit -> Some permit
        | ReconcileProgram.ReconcileWake.RetryWake
        | ReconcileProgram.ReconcileWake.FailureWake
        | ReconcileProgram.ReconcileWake.AbortWake -> None

    let private observeIfPresent
        (isCurrent: SessionId -> int -> bool)
        (observeSnapshot: SessionId -> SessionMessage list -> Task)
        (sessionId: SessionId)
        (generation: int)
        (lastSnapshot: SessionMessage list option)
        : Task =
        task {
            match lastSnapshot, isCurrent sessionId generation with
            | Some messages, true -> do! observeSnapshot sessionId messages
            | _ -> ()
        }

    let private maybeRecordMaps
        (shouldPublish: bool)
        (maps: ReconcileProgram.PublishMaps)
        (isCurrent: SessionId -> int -> bool)
        (recordMaps: SessionId -> ReconcileProgram.PublishMaps -> unit)
        (sessionId: SessionId)
        (generation: int)
        : unit =
        if shouldPublish && isCurrent sessionId generation then
            recordMaps sessionId maps

    let private publishResolvedTurn
        (isCurrent: SessionId -> int -> bool)
        (recordMaps: SessionId -> ReconcileProgram.PublishMaps -> unit)
        (onTurn: ReconciledTurnContext -> Task)
        (sessionId: SessionId)
        (generation: int)
        (wake: ReconcileProgram.ReconcileWake)
        (shouldPublish: bool)
        (maps: ReconcileProgram.PublishMaps)
        (value: ReconcileProgram.PublishTurn)
        (turns: Map<string, ReconciledTurn>)
        (delivery: ReconciledTurnDelivery)
        : Task =
        task {
            match Map.tryFind (ReconcileProgram.consumeKey value) turns with
            | None -> logError "RECONCILE-PUBLISH" "publish token missing from observed turns"
            | Some reconciled ->
                do!
                    onTurn
                        { Turn = reconciled
                          Quiescence = quiescenceOf wake
                          Delivery = delivery }

                maybeRecordMaps shouldPublish maps isCurrent recordMaps sessionId generation
        }

    let private publishPresentTurn
        (isCurrent: SessionId -> int -> bool)
        (recordMaps: SessionId -> ReconcileProgram.PublishMaps -> unit)
        (observeSnapshot: SessionId -> SessionMessage list -> Task)
        (onTurn: ReconciledTurnContext -> Task)
        (sessionId: SessionId)
        (generation: int)
        (wake: ReconcileProgram.ReconcileWake)
        (maps: ReconcileProgram.PublishMaps)
        (value: ReconcileProgram.PublishTurn)
        (turns: Map<string, ReconciledTurn>)
        (lastSnapshot: SessionMessage list option)
        : Task =
        task {
            let decision = ReconcileProgram.publishDecision maps value
            let delivery = deliveryOf decision.shouldPublish wake

            match delivery, isCurrent sessionId generation with
            | Some resolved, true ->
                do!
                    publishResolvedTurn
                        isCurrent
                        recordMaps
                        onTurn
                        sessionId
                        generation
                        wake
                        decision.shouldPublish
                        decision.maps
                        value
                        turns
                        resolved
            | _ -> ()

            do! observeIfPresent isCurrent observeSnapshot sessionId generation lastSnapshot
        }

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
            | None -> do! observeIfPresent isCurrent observeSnapshot sessionId generation lastSnapshot
            | Some value ->
                do!
                    publishPresentTurn
                        isCurrent
                        recordMaps
                        observeSnapshot
                        onTurn
                        sessionId
                        generation
                        wake
                        maps
                        value
                        turns
                        lastSnapshot
        }

    let private publishableOf
        (evidence: ReconcileProgram.ReconcileEvidence)
        (candidate: ReconcileProgram.PublishTurn option)
        : ReconcileProgram.PublishTurn option =
        match evidence with
        | ReconcileProgram.ReconcileEvidence.Terminal observed -> observed.PublishTurn
        | ReconcileProgram.ReconcileEvidence.Unknown(Some observed) -> observed.PublishTurn
        | ReconcileProgram.ReconcileEvidence.Provisional observed -> observed.PublishTurn
        | _ -> candidate

    let private rereadCandidate
        (clearCandidate: bool)
        (evidence: ReconcileProgram.ReconcileEvidence)
        (candidate: ReconcileProgram.PublishTurn option)
        : ReconcileProgram.PublishTurn option =
        match clearCandidate, evidence with
        | true, _ -> None
        | false, ReconcileProgram.ReconcileEvidence.Provisional observed -> observed.PublishTurn
        | false, _ -> candidate

    let private observedTurnsOf
        (turn: ReconciledTurn option)
        (turns: Map<string, ReconciledTurn>)
        : Map<string, ReconciledTurn> =
        match turn with
        | Some value -> Map.add (ReconcileProgram.consumeKey (publishTurnOf value)) value turns
        | None -> turns

    type private MaterializeContinuation =
        | ObserveSnapshot of SessionMessage list option
        | PublishTurn of
            publishable: ReconcileProgram.PublishTurn option *
            turns: Map<string, ReconciledTurn> *
            messages: SessionMessage list
        | Continue of
            rereads: int *
            errors: int *
            candidate: ReconcileProgram.PublishTurn option *
            turns: Map<string, ReconciledTurn> *
            snapshot: SessionMessage list option

    let private classifySnapshotError
        (consecutiveErrors: int)
        (maxErrors: int)
        (rereadsRemaining: int)
        (candidate: ReconcileProgram.PublishTurn option)
        (turns: Map<string, ReconciledTurn>)
        (lastSnapshot: SessionMessage list option)
        (error: string)
        : MaterializeContinuation =
        logError "RECONCILE-SNAPSHOT" (sprintf "snapshot failed: %s" (string error))
        let nextErrors = consecutiveErrors + 1

        if nextErrors >= maxErrors then
            // StopPass: keep Dirty for next host signal; errors do not consume causal budget.
            ObserveSnapshot lastSnapshot
        else
            Continue(rereadsRemaining, nextErrors, candidate, turns, lastSnapshot)

    let private classifySnapshotOk
        (wake: ReconcileProgram.ReconcileWake)
        (rereadsRemaining: int)
        (candidate: ReconcileProgram.PublishTurn option)
        (activeBinding: ActiveRunBinding)
        (turns: Map<string, ReconciledTurn>)
        (messages: SessionMessage list)
        : MaterializeContinuation =
        let turn = TurnReconcile.reconcile messages activeBinding
        let evidence = evidenceOf turn
        let observedTurns = observedTurnsOf turn turns
        let decision = ReconcileProgram.decideStep wake rereadsRemaining evidence

        match decision with
        | ReconcileProgram.ReconcileDecision.Publish ->
            // Stable observation handoff only (rabbit §7).
            // Unknown under IdleWake Publishes the observed turn
            // as-is; TurnWorkflow / InteractionRepair owns any
            // missing-final-report repair (GLORY-070), gated on
            // the pass's quiescence evidence.
            PublishTurn(publishableOf evidence candidate, observedTurns, messages)
        | ReconcileProgram.ReconcileDecision.StopPass -> ObserveSnapshot(Some messages)
        | ReconcileProgram.ReconcileDecision.Reread(clearCandidate, remaining) ->
            Continue(remaining, 0, rereadCandidate clearCandidate evidence candidate, observedTurns, Some messages)

    let private continueIfCurrent
        (isCurrent: SessionId -> int -> bool)
        (sessionId: SessionId)
        (generation: int)
        (next: unit -> Task)
        : Task =
        task {
            if isCurrent sessionId generation then
                return! next ()
            else
                return ()
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

                return!
                    resumeAfterSnapshot
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
                        consecutiveErrors
                        candidate
                        maps
                        activeBinding
                        turns
                        lastSnapshot
                        result
        }

    and private resumeAfterSnapshot
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
        (result: Result<SessionMessage list, string>)
        : Task =
        task {
            if not (isCurrent sessionId generation) then
                return ()
            else
                return!
                    applyContinuation
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
                        maps
                        activeBinding
                        (classifySnapshotResult
                            wake
                            rereadsRemaining
                            consecutiveErrors
                            maxErrors
                            candidate
                            activeBinding
                            turns
                            lastSnapshot
                            result)
        }

    and private classifySnapshotResult
        (wake: ReconcileProgram.ReconcileWake)
        (rereadsRemaining: int)
        (consecutiveErrors: int)
        (maxErrors: int)
        (candidate: ReconcileProgram.PublishTurn option)
        (activeBinding: ActiveRunBinding)
        (turns: Map<string, ReconciledTurn>)
        (lastSnapshot: SessionMessage list option)
        (result: Result<SessionMessage list, string>)
        : MaterializeContinuation =
        match result with
        | Error error ->
            classifySnapshotError
                consecutiveErrors
                maxErrors
                rereadsRemaining
                candidate
                turns
                lastSnapshot
                error
        | Ok messages -> classifySnapshotOk wake rereadsRemaining candidate activeBinding turns messages

    and private applyContinuation
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
        (maps: ReconcileProgram.PublishMaps)
        (activeBinding: ActiveRunBinding)
        (continuation: MaterializeContinuation)
        : Task =
        task {
            match continuation with
            | ObserveSnapshot snap -> do! observeIfPresent isCurrent observeSnapshot sessionId generation snap
            | PublishTurn(publishable, observedTurns, messages) ->
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
            | Continue(rereads, errors, nextCandidate, nextTurns, snap) ->
                do!
                    continueIfCurrent isCurrent sessionId generation (fun () ->
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
                            rereads
                            errors
                            nextCandidate
                            maps
                            activeBinding
                            nextTurns
                            snap)
        }

    let private observeIdleSnapshot
        (snapshot: ISessionSnapshotPort)
        (isCurrent: SessionId -> int -> bool)
        (observeSnapshot: SessionId -> SessionMessage list -> Task)
        (sessionId: SessionId)
        (generation: int)
        : Task =
        task {
            let! result = snapshot.GetMessages sessionId

            match isCurrent sessionId generation, result with
            | false, _ -> ()
            | true, Ok messages -> do! observeSnapshot sessionId messages
            | true, Error error -> logError "RECONCILE-SNAPSHOT" (sprintf "snapshot failed: %s" (string error))
        }

    let private runWithBinding
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
        match activeBinding with
        | None ->
            // HOST-006: still read + observe when no active run.
            observeIdleSnapshot snapshot isCurrent observeSnapshot sessionId generation
        | Some bound ->
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
                return!
                    runWithBinding
                        snapshot
                        isCurrent
                        isCleared
                        mapsFor
                        recordMaps
                        wake
                        observeSnapshot
                        onTurn
                        maxRereads
                        maxErrors
                        activeBinding
                        sessionId
                        generation
        }
