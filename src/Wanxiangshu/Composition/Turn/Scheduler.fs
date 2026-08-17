namespace Wanxiangshu.Composition.Turn

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
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
open Wanxiangshu.Composition.Durable
open Wanxiangshu.OpenCode

/// Direct-CE reconcile scheduler (FLOW-001 / PR4).
/// Owns queue / generation / single-flight / clear-session runtime state.
/// Pass body lives in ReconcilePass — Scheduler only drains and dispatches.
module Reconciler =

    [<RequireQualifiedAccess>]
    type private Dispatch =
        | Start of generation: int
        | Enqueued
        | Stopped

    [<Emit("console.error($0, $1)")>]
    let private logError (prefix: string) (message: string) : unit = jsNative

    type Scheduler
        (
            snapshot: ISessionSnapshotPort,
            binding: TurnBinding.Store,
            onTurn: ReconciledTurnContext -> Task,
            ?onDeleted: SessionId -> unit,
            ?projection: (SessionId -> AgentProjectionSet option),
            ?onSnapshot: SessionId -> SessionMessage list -> Task,
            ?durableUnavailable: unit -> bool,
            ?maxCausalRereads: int,
            ?maxConsecutiveErrors: int
        ) as this =

        let gate = obj ()
        // When a signal arrives while a drain is active, the work is queued
        // here; releaseAfterPass checks it to re-drain after the current pass.
        // Same single-flight model as runningDrains (global task count) but
        // scoped per session.
        // DSL-MUTABLE: resource — per-session single-flight admission queue.
        let queued = Dictionary<string, int>()
        // (HasFlight guard). startOrEnqueue checks active.ContainsKey to decide
        // whether to start a new drain task or enqueue. Only one drain task per
        // session at a time — the per-session equivalent of runningDrains.
        // DSL-MUTABLE: resource — per-session single-flight admission latch.
        let active = Dictionary<string, int>()
        // Set by ClearSession, cleared by BindUserMessage/BindActiveRun.
        // RunPass polls isCleared to skip drains for cleared sessions.
        // DSL-MUTABLE: resource — per-session cleared-session flag.
        let cleared = HashSet<string>()
        // store. ClearSession bumps the generation to invalidate in-flight
        // drains; isCurrent (polled deep inside ReconcilePass.run) compares the
        // generation captured at drain start against the current value here and
        // aborts stale drains. The generation value itself already flows as a CE
        // recursion parameter through Drain/RunPass/DrainAfterPass — this
        // Dictionary is the external invalidation authority that ClearSession
        // mutates, not a drain-pass program counter. Without it, ClearSession
        // would have no mechanism to cancel running drains.
        // DSL-MUTABLE: resource — per-session cooperative cancellation token store.
        let generations = Dictionary<string, int>()
        // DSL-MUTABLE: resource — per-session published reconcile maps cache.
        // recordMaps stores publish output; mapsFor retrieves it for re-drain.
        let published = Dictionary<string, ReconcileProgram.PublishMaps>()
        // DSL-MUTABLE: resource — shutdown admission latch.
        let mutable accepting = true
        // DSL-MUTABLE: resource — count of already-started physical drain tasks.
        let mutable runningDrains = 0
        // DSL-MUTABLE: single-flight — shared waiter for scheduler shutdown drain.
        let mutable stopWaiter: TaskCompletionSource<unit> option = None
        // Never consumed: a drain-resume re-run needs the same wake, and a newer
        // signal simply overwrites it. A session with no recorded wake defaults
        // to RetryWake (no idle rights — the safe side).
        // DSL-MUTABLE: resource — per-session last-dispatch wake.
        let wakes = Dictionary<string, ReconcileProgram.ReconcileWake>()
        let resolveProjection = defaultArg projection (fun _ -> None)
        let onDeleted = defaultArg onDeleted ignore
        let isDurableUnavailable = defaultArg durableUnavailable (fun () -> false)

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

        let startOrEnqueue (key: string) (generation: int) =
            if active.ContainsKey key then
                Dispatch.Enqueued
            else
                active.[key] <- generation
                runningDrains <- runningDrains + 1
                Dispatch.Start generation

        let closeAdmission () =
            accepting <- false
            queued.Clear()

        let dispatch (sessionId: SessionId) (wake: ReconcileProgram.ReconcileWake) =
            lock gate (fun () ->
                if not accepting || isDurableUnavailable () then
                    closeAdmission ()
                    Dispatch.Stopped
                else
                    let key = SessionId.value sessionId
                    let generation = currentGeneration sessionId
                    wakes.[key] <- wake
                    queued.[key] <- generation
                    startOrEnqueue key generation)

        let finishDrain () =
            lock gate (fun () ->
                runningDrains <- runningDrains - 1

                if not accepting && runningDrains = 0 then
                    stopWaiter
                    |> Option.iter (fun waiter -> AsyncSupport.trySetResult waiter () |> ignore))

        let currentWake (sessionId: SessionId) =
            lock gate (fun () ->
                let key = SessionId.value sessionId

                match wakes.TryGetValue key with
                | true, wake -> wake
                | false, _ -> ReconcileProgram.ReconcileWake.RetryWake)

        let takeQueuedWork (sessionId: SessionId) (generation: int) =
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
                true
            else
                false

        let takeWork (sessionId: SessionId) (generation: int) =
            lock gate (fun () ->
                if isDurableUnavailable () then
                    closeAdmission ()
                    false
                else
                    takeQueuedWork sessionId generation)

        let releaseActive key generation =
            match active.TryGetValue key with
            | true, activeGeneration when activeGeneration = generation -> active.Remove(key) |> ignore
            | _ -> ()

        let releaseAvailableAfterPass (sessionId: SessionId) (generation: int) =
            let key = SessionId.value sessionId
            let activeState = active.TryGetValue key
            let queuedState = queued.TryGetValue key

            match activeState, queuedState with
            | (true, activeGeneration), (true, queuedGeneration) when
                activeGeneration = generation
                && queuedGeneration = generation
                && isCurrent sessionId generation
                ->
                true
            | (true, activeGeneration), _ when activeGeneration = generation ->
                active.Remove(key) |> ignore
                false
            | _ -> false

        let releaseAfterPass (sessionId: SessionId) (generation: int) =
            lock gate (fun () ->
                if isDurableUnavailable () then
                    closeAdmission ()
                    releaseActive (SessionId.value sessionId) generation
                    false
                else
                    releaseAvailableAfterPass sessionId generation)

        let mapsFor (sessionId: SessionId) =
            lock gate (fun () ->
                let key = SessionId.value sessionId

                match published.TryGetValue key with
                | true, maps -> maps
                | false, _ -> ReconcileProgram.publishMapsEmpty ())

        let recordMaps (sessionId: SessionId) (maps: ReconcileProgram.PublishMaps) =
            lock gate (fun () -> published.[SessionId.value sessionId] <- maps)

        member private _.RunPass(sessionId: SessionId, generation: int) : Task =
            task {
                if
                    isDurableUnavailable ()
                    || not (isCurrent sessionId generation)
                    || isCleared sessionId
                then
                    return ()
                else
                    let wake = currentWake sessionId

                    let activeBinding =
                        binding.ActiveRunBinding(sessionId, ?projection = resolveProjection sessionId)

                    return!
                        ReconcilePass.run
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

        member private this.DrainAfterPass(sessionId: SessionId, generation: int) : Task =
            task {
                if isCurrent sessionId generation then
                    return! this.Drain(sessionId, generation)
                else
                    return ()
            }

        member private this.Drain(sessionId: SessionId, generation: int) : Task =
            task {
                if takeWork sessionId generation then
                    do! this.RunPass(sessionId, generation)
                    return! this.DrainAfterPass(sessionId, generation)
                else
                    return ()
            }

        member private this.Run(sessionId: SessionId, generation: int) : Task =
            task {
                try
                    do! this.Drain(sessionId, generation)
                with error ->
                    logError "RECONCILE-SCHEDULER" error.Message

                if releaseAfterPass sessionId generation then
                    return! this.Run(sessionId, generation)
                else
                    return ()
            }

        member private this.RunTracked(sessionId: SessionId, generation: int) : Task =
            task {
                try
                    do! this.Run(sessionId, generation)
                finally
                    finishDrain ()
            }

        member _.Kick(sessionId: SessionId, wake: ReconcileProgram.ReconcileWake) : unit =
            match dispatch sessionId wake with
            | Dispatch.Start generation -> this.RunTracked(sessionId, generation) |> ignore
            | Dispatch.Enqueued
            | Dispatch.Stopped -> ()

        member private _.DrainWaitTask() : Task =
            match stopWaiter with
            | Some waiter -> waiter.Task :> Task
            | None ->
                let waiter =
                    TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                stopWaiter <- Some waiter
                waiter.Task :> Task

        member this.StopAndDrain() : Task =
            lock gate (fun () ->
                closeAdmission ()

                if runningDrains = 0 then
                    Task.FromResult(()) :> Task
                else
                    this.DrainWaitTask())

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

        member _.BindPhysicalUserMaterial(sessionId: SessionId, physical: PhysicalUserMessageId) =
            binding.BindPhysicalUserMaterial(sessionId, physical)

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
