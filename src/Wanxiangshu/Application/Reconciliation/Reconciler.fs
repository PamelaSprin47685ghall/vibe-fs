namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open Wanxiangshu.Journal

/// Direct-CE reconcile scheduler (FLOW-001 / PR4).
/// Owns queue / generation / single-flight / clear-session runtime state.
/// Pass body is task CE over pure Domain decisions — no Command/Reply AST.
module Reconciler =

    [<RequireQualifiedAccess>]
    type private Dispatch =
        | Start of generation: int
        | Enqueued

    [<RequireQualifiedAccess>]
    type private Work =
        | Wake
        | Drained

    [<RequireQualifiedAccess>]
    type private Release =
        | ResumeDrain
        | Released

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

    type Scheduler
        (
            snapshot: ISessionSnapshotPort,
            binding: TurnBinding.Store,
            onTurn: ReconciledTurnContext -> Task,
            ?onDeleted: SessionId -> unit,
            ?projection: (SessionId -> AgentProjectionSet option),
            ?onSnapshot: SessionId -> SessionMessage list -> Task,
            ?maxCausalRereads: int,
            ?maxConsecutiveErrors: int
        ) as this =

        let gate = obj ()
        let queued = Dictionary<string, int>()
        let active = Dictionary<string, int>()
        let cleared = HashSet<string>()
        let generations = Dictionary<string, int>()
        let published = Dictionary<string, ReconcileProgram.PublishMaps>()
        // The wake of the most recent dispatch for a session. Never consumed:
        // a ResumeDrain re-run needs the same wake, and a newer signal simply
        // overwrites it. A session with no recorded wake defaults to RetryWake
        // (no idle rights — the safe side).
        let wakes = Dictionary<string, ReconcileProgram.ReconcileWake>()
        let resolveProjection = defaultArg projection (fun _ -> None)
        let onDeleted = defaultArg onDeleted ignore

        let observeSnapshot =
            defaultArg onSnapshot (fun _ _ -> AsyncSupport.completedTask ())

        let maxRereads = defaultArg maxCausalRereads 3
        let maxErrors = defaultArg maxConsecutiveErrors 5

        let isCleared (sessionId: SessionId) =
            lock gate (fun () -> cleared.Contains(SessionId.value sessionId))

        let currentGeneration (sessionId: SessionId) =
            lock gate (fun () ->
                match generations.TryGetValue(SessionId.value sessionId) with
                | true, value -> value
                | false, _ -> 0)

        let isCurrent (sessionId: SessionId) (generation: int) =
            currentGeneration sessionId = generation

        let dispatch (sessionId: SessionId) (wake: ReconcileProgram.ReconcileWake) =
            lock gate (fun () ->
                let key = SessionId.value sessionId
                let generation = currentGeneration sessionId
                wakes.[key] <- wake
                queued.[key] <- generation

                if active.ContainsKey key then
                    Dispatch.Enqueued
                else
                    active.[key] <- generation
                    Dispatch.Start generation)

        let currentWake (sessionId: SessionId) =
            lock gate (fun () ->
                let key = SessionId.value sessionId

                match wakes.TryGetValue key with
                | true, wake -> wake
                | false, _ -> ReconcileProgram.ReconcileWake.RetryWake)

        let takeWork (sessionId: SessionId) (generation: int) =
            lock gate (fun () ->
                let key = SessionId.value sessionId

                let isActive =
                    match active.TryGetValue key with
                    | true, activeGeneration -> activeGeneration = generation
                    | false, _ -> false

                let isQueued =
                    match queued.TryGetValue key with
                    | true, queuedGeneration -> queuedGeneration = generation
                    | false, _ -> false

                if isCurrent sessionId generation && isActive && isQueued then
                    queued.Remove(key) |> ignore
                    Work.Wake
                else
                    Work.Drained)

        let releaseAfterPass (sessionId: SessionId) (generation: int) =
            lock gate (fun () ->
                let key = SessionId.value sessionId

                match active.TryGetValue key with
                | true, activeGeneration when activeGeneration = generation ->
                    match queued.TryGetValue key with
                    | true, queuedGeneration when queuedGeneration = generation && isCurrent sessionId generation ->
                        Release.ResumeDrain
                    | _ ->
                        active.Remove(key) |> ignore
                        Release.Released
                | _ -> Release.Released)

        let mapsFor (sessionId: SessionId) =
            lock gate (fun () ->
                let key = SessionId.value sessionId

                match published.TryGetValue key with
                | true, maps -> maps
                | false, _ -> ReconcileProgram.publishMapsEmpty ())

        let recordMaps (sessionId: SessionId) (maps: ReconcileProgram.PublishMaps) =
            lock gate (fun () -> published.[SessionId.value sessionId] <- maps)

        let publishIfAllowed
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

                    if decision.shouldPublish && isCurrent sessionId generation then
                        match Map.tryFind (ReconcileProgram.consumeKey value) turns with
                        | Some reconciled ->
                            let quiescence =
                                match wake with
                                | ReconcileProgram.ReconcileWake.IdleWake permit -> Some permit
                                | ReconcileProgram.ReconcileWake.RetryWake
                                | ReconcileProgram.ReconcileWake.FailureWake
                                | ReconcileProgram.ReconcileWake.AbortWake -> None

                            let context: ReconciledTurnContext =
                                { Turn = reconciled
                                  Quiescence = quiescence }

                            do! onTurn context

                            if isCurrent sessionId generation then
                                recordMaps sessionId decision.maps
                        | None -> logError "RECONCILE-PUBLISH" "publish token missing from observed turns"

                    if isCurrent sessionId generation then
                        match lastSnapshot with
                        | Some messages -> do! observeSnapshot sessionId messages
                        | None -> ()
            }

        let rec materializeActive
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
                                        | ReconcileProgram.ReconcileEvidence.Provisional observed ->
                                            observed.PublishTurn
                                        | _ -> candidate

                                if isCurrent sessionId generation then
                                    return!
                                        materializeActive
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

        member private _.RunPass(sessionId: SessionId, generation: int) : Task =
            task {
                if not (isCurrent sessionId generation) || isCleared sessionId then
                    return ()
                else
                    let wake = currentWake sessionId

                    let activeBinding =
                        binding.ActiveRunBinding(sessionId, ?projection = resolveProjection sessionId)

                    match activeBinding with
                    | None ->
                        // HOST-006: still read + observe when no active run.
                        let! result = snapshot.GetMessages sessionId

                        if isCurrent sessionId generation then
                            match result with
                            | Ok messages -> do! observeSnapshot sessionId messages
                            | Error error ->
                                logError "RECONCILE-SNAPSHOT" (sprintf "snapshot failed: %s" (string error))
                    | Some bound ->
                        return!
                            materializeActive
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

        member private this.Drain(sessionId: SessionId, generation: int) : Task =
            task {
                match takeWork sessionId generation with
                | Work.Drained -> return ()
                | Work.Wake ->
                    do! this.RunPass(sessionId, generation)

                    if isCurrent sessionId generation then
                        return! this.Drain(sessionId, generation)
                    else
                        return ()
            }

        member private this.Run(sessionId: SessionId, generation: int) : Task =
            task {
                try
                    do! this.Drain(sessionId, generation)
                with error ->
                    logError "RECONCILE-SCHEDULER" error.Message

                match releaseAfterPass sessionId generation with
                | Release.ResumeDrain -> return! this.Run(sessionId, generation)
                | Release.Released -> return ()
            }

        member _.Kick(sessionId: SessionId, wake: ReconcileProgram.ReconcileWake) : unit =
            match dispatch sessionId wake with
            | Dispatch.Start generation -> this.Run(sessionId, generation) |> ignore
            | Dispatch.Enqueued -> ()

        member _.SignalIdle(sessionId: SessionId, permit: QuiescencePermit) : unit =
            this.Kick(sessionId, ReconcileProgram.ReconcileWake.IdleWake permit)

        member this.Signal(signal: HostSignal) : unit =
            match signal with
            | SessionIdle sessionId -> this.Kick(sessionId, ReconcileProgram.ReconcileWake.RetryWake)
            | ProviderFailure(sessionId, _) -> this.Kick(sessionId, ReconcileProgram.ReconcileWake.FailureWake)
            | ProviderRetry retry -> this.Kick(retry.SessionId, ReconcileProgram.ReconcileWake.RetryWake)
            | SessionDeleted(sessionId, _) -> this.ClearSession(sessionId)
            // HOST-002/004: an operator abort is a typed wake, not a failure.
            // AbortWake holds no idle rights (quiescence was already revoked via
            // RevokeCurrentAttempt), and decideStep refuses to Publish Unknown /
            // Provisional under it so business cannot mint InteractionRepair.
            // The genuine TurnAborted terminal publishes normally; Unknown /
            // Provisional StopPass instead of resurrecting an idle-derived
            // continuation.
            | AttemptAborted sessionId -> this.Kick(sessionId, ReconcileProgram.ReconcileWake.AbortWake)

        member _.BindUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId, ?agentRole: Role) =
            lock gate (fun () -> cleared.Remove(SessionId.value sessionId) |> ignore)
            binding.BindUserMessage(sessionId, physical, ?agentRole = agentRole)

        member _.BindContinuationUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId) =
            binding.BindContinuationUserMessage(sessionId, physical)

        member _.BindActiveRun(value: ActiveRunBinding) =
            lock gate (fun () -> cleared.Remove(SessionId.value value.SessionId) |> ignore)
            binding.BindActiveRun(value)

        member _.TryPhysicalUserMessage(sessionId: SessionId) =
            binding.TryPhysicalUserMessage(sessionId)

        member _.RootBindings = binding.UserMessageBindings

        member _.ClearSession(sessionId: SessionId) : unit =
            lock gate (fun () ->
                let key = SessionId.value sessionId

                let generation =
                    match generations.TryGetValue key with
                    | true, value -> value + 1
                    | false, _ -> 1

                generations.[key] <- generation
                cleared.Add(key) |> ignore
                queued.Remove(key) |> ignore
                active.Remove(key) |> ignore
                published.Remove(key) |> ignore
                wakes.Remove(key) |> ignore)

            binding.ClearSession(sessionId)
            onDeleted sessionId
