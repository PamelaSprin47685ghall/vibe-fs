namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
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

    let private delayMs (ms: int) : Task<unit> =
        if ms <= 0 then
            Task.FromResult(())
        else
            emitJsExpr ms "new Promise(res => setTimeout(res, $0))"

    [<Emit("console.error($0, $1)")>]
    let private logError (prefix: string) (message: string) : unit = jsNative

    let private domainOutcome (outcome: TurnOutcome) : ReconcileProgram.TurnOutcome =
        match outcome with
        | TurnInProgress -> ReconcileProgram.TurnInProgress
        | TurnNeedsContinuation reason -> ReconcileProgram.TurnNeedsContinuation reason
        | TurnCompleted -> ReconcileProgram.TurnCompleted
        | TurnAborted reason -> ReconcileProgram.TurnAborted reason
        | TurnFailed error -> ReconcileProgram.TurnFailed error
        | TurnUnknown -> ReconcileProgram.TurnUnknown

    let private publishTurnOf (turn: ReconciledTurn) : ReconcileProgram.PublishTurn =
        { SessionId = turn.SessionId
          PhysicalUserMessageId = turn.PhysicalUserMessageId
          ProviderRun = turn.ProviderRun
          Outcome = domainOutcome turn.Outcome }

    let private evidenceOf (turn: ReconciledTurn option) : ReconcileProgram.ReconcileEvidence =
        match turn with
        | None -> ReconcileProgram.ReconcileEvidence.NoTurn
        | Some value ->
            let observed = ReconcileProgram.observedTurn (publishTurnOf value)

            match value.Outcome with
            | TurnCompleted
            | TurnAborted _
            | TurnFailed _ -> ReconcileProgram.ReconcileEvidence.Terminal observed
            | TurnInProgress
            | TurnNeedsContinuation _ -> ReconcileProgram.ReconcileEvidence.Provisional observed
            | TurnUnknown -> ReconcileProgram.ReconcileEvidence.Unknown(Some observed)

    type Scheduler
        (
            snapshot: ISessionSnapshotPort,
            binding: TurnBinding.Store,
            onTurn: ReconciledTurn -> Task,
            ?onDeleted: SessionId -> unit,
            ?projection: (SessionId -> AgentProjectionSet option),
            ?onSnapshot: SessionId -> SessionMessage list -> Task,
            ?backoffDelaysMs: int array,
            ?maxBudgetMs: int
        ) as this =

        let gate = obj ()
        let queued = Dictionary<string, int>()
        let active = Dictionary<string, int>()
        let cleared = HashSet<string>()
        let generations = Dictionary<string, int>()
        let published = Dictionary<string, ReconcileProgram.PublishMaps>()
        let resolveProjection = defaultArg projection (fun _ -> None)
        let onDeleted = defaultArg onDeleted ignore

        let observeSnapshot =
            defaultArg onSnapshot (fun _ _ -> AsyncSupport.completedTask ())

        let delays =
            defaultArg backoffDelaysMs [| 50; 100; 250; 500; 1000; 2000; 3000; 5000 |]

        let budget = defaultArg maxBudgetMs 30_000

        let isCleared (sessionId: SessionId) =
            lock gate (fun () -> cleared.Contains(SessionId.value sessionId))

        let currentGeneration (sessionId: SessionId) =
            lock gate (fun () ->
                match generations.TryGetValue(SessionId.value sessionId) with
                | true, value -> value
                | false, _ -> 0)

        let isCurrent (sessionId: SessionId) (generation: int) =
            currentGeneration sessionId = generation

        let dispatch (sessionId: SessionId) =
            lock gate (fun () ->
                let key = SessionId.value sessionId
                let generation = currentGeneration sessionId
                queued.[key] <- generation

                if active.ContainsKey key then
                    Dispatch.Enqueued
                else
                    active.[key] <- generation
                    Dispatch.Start generation)

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
                            do! onTurn reconciled

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
            (budgetRemaining: int)
            (backoffIndex: int)
            (candidate: ReconcileProgram.PublishTurn option)
            (maps: ReconcileProgram.PublishMaps)
            (activeBinding: ActiveRunBinding)
            (turns: Map<string, ReconciledTurn>)
            (lastSnapshot: SessionMessage list option)
            : Task =
            task {
                if not (isCurrent sessionId generation) then
                    return ()
                elif budgetRemaining <= 0 then
                    let evidence = ReconcileProgram.ReconcileEvidence.BudgetExhausted candidate.IsSome

                    match ReconcileProgram.decideStep evidence with
                    | ReconcileProgram.ReconcileDecision.Publish ->
                        do! publishIfAllowed sessionId generation maps candidate turns lastSnapshot
                    | ReconcileProgram.ReconcileDecision.StopPass
                    | ReconcileProgram.ReconcileDecision.RereadWithBackoff _ ->
                        match lastSnapshot with
                        | Some messages when isCurrent sessionId generation -> do! observeSnapshot sessionId messages
                        | _ -> ()
                else if isCleared sessionId || not (isCurrent sessionId generation) then
                    return ()
                else
                    let! result = snapshot.GetMessages sessionId

                    if not (isCurrent sessionId generation) then
                        return ()
                    else
                        match result with
                        | Error error ->
                            logError "RECONCILE-SNAPSHOT" (sprintf "snapshot failed: %s" (string error))
                            let nextIdx = ReconcileProgram.nextBackoffIndex backoffIndex false
                            let delay = ReconcileProgram.pickDelay delays backoffIndex budgetRemaining

                            if delay <= 0 || not (isCurrent sessionId generation) then
                                return ()
                            else
                                do! delayMs delay

                                if isCurrent sessionId generation then
                                    return!
                                        materializeActive
                                            sessionId
                                            generation
                                            (budgetRemaining - delay)
                                            nextIdx
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

                            let afterOk = ReconcileProgram.nextBackoffIndex backoffIndex true
                            let decision = ReconcileProgram.decideStep evidence

                            match decision with
                            | ReconcileProgram.ReconcileDecision.Publish ->
                                let publishable =
                                    match evidence with
                                    | ReconcileProgram.ReconcileEvidence.Terminal observed -> observed.PublishTurn
                                    | ReconcileProgram.ReconcileEvidence.BudgetExhausted _ -> candidate
                                    | _ -> candidate

                                do! publishIfAllowed sessionId generation maps publishable observedTurns (Some messages)
                            | ReconcileProgram.ReconcileDecision.StopPass ->
                                if isCurrent sessionId generation then
                                    do! observeSnapshot sessionId messages
                            | ReconcileProgram.ReconcileDecision.RereadWithBackoff clearCandidate ->
                                let candidate' =
                                    if clearCandidate then
                                        None
                                    else
                                        match evidence with
                                        | ReconcileProgram.ReconcileEvidence.Provisional observed ->
                                            observed.PublishTurn
                                        | _ -> candidate

                                let delay = ReconcileProgram.pickDelay delays afterOk budgetRemaining
                                let nextIdx = afterOk + 1

                                if delay <= 0 then
                                    if isCurrent sessionId generation then
                                        do! observeSnapshot sessionId messages
                                else
                                    do! delayMs delay

                                    if isCurrent sessionId generation then
                                        return!
                                            materializeActive
                                                sessionId
                                                generation
                                                (budgetRemaining - delay)
                                                nextIdx
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
                                budget
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

        member _.Kick(sessionId: SessionId) : unit =
            match dispatch sessionId with
            | Dispatch.Start generation -> this.Run(sessionId, generation) |> ignore
            | Dispatch.Enqueued -> ()

        member this.Signal(signal: HostSignal) : unit =
            match signal with
            | SessionIdle sessionId
            | ProviderFailure(sessionId, _) -> this.Kick(sessionId)
            | ProviderRetry retry -> this.Kick(retry.SessionId)
            | SessionDeleted sessionId -> this.ClearSession(sessionId)

        member _.BindUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId, ?agentRole: AgentRole) =
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
                published.Remove(key) |> ignore)

            binding.ClearSession(sessionId)
            onDeleted sessionId
