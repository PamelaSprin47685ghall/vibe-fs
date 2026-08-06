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

/// Production interpreter for Domain.ReconcileProgram. Host signals enqueue
/// observation work; the Domain program alone determines the next action.
module ReconcileInterpreter =

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

    let private publishTurn (turn: ReconciledTurn) : ReconcileProgram.PublishTurn =
        { SessionId = turn.SessionId
          PhysicalUserMessageId = turn.PhysicalUserMessageId
          ProviderRun = turn.ProviderRun
          Outcome = domainOutcome turn.Outcome }

    let private evidenceOf (turn: ReconciledTurn option) : ReconcileProgram.ReconcileEvidence =
        match turn with
        | None -> ReconcileProgram.ReconcileEvidence.NoTurn
        | Some value ->
            let observed = ReconcileProgram.observedTurn (publishTurn value)

            match value.Outcome with
            | TurnCompleted
            | TurnAborted _
            | TurnFailed _ -> ReconcileProgram.ReconcileEvidence.Terminal observed
            | TurnInProgress
            | TurnNeedsContinuation _ -> ReconcileProgram.ReconcileEvidence.Provisional observed
            | TurnUnknown -> ReconcileProgram.ReconcileEvidence.Unknown(Some observed)

    type Interpreter
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

        member private _.Interpret
            (sessionId: SessionId, generation: int, program: ReconcileProgram.ReconcileProgram)
            : Task =
            let rec go
                (current: ReconcileProgram.ReconcileProgram)
                (activeBinding: ActiveRunBinding option)
                (lastSnapshot: SessionMessage list option)
                (turns: Map<string, ReconciledTurn>)
                : Task =
                task {
                    match current with
                    | ReconcileProgram.Return() -> return ()
                    | ReconcileProgram.Step(command, next) ->
                        if not (isCurrent sessionId generation) then
                            return ()
                        else
                            match command with
                            | ReconcileProgram.ReconcileCommand.ReadActiveBinding _ ->
                                let found =
                                    if not (isCurrent sessionId generation) || isCleared sessionId then
                                        None
                                    else
                                        binding.ActiveRunBinding(sessionId, ?projection = resolveProjection sessionId)

                                let reply =
                                    match found with
                                    | Some _ -> ReconcileProgram.ReconcileReply.BindingPresent
                                    | None -> ReconcileProgram.ReconcileReply.BindingAbsent

                                return! go (next reply) found lastSnapshot turns

                            | ReconcileProgram.ReconcileCommand.ReadSnapshot _ ->
                                if not (isCurrent sessionId generation) || isCleared sessionId then
                                    return!
                                        go
                                            (next (
                                                ReconcileProgram.ReconcileReply.SnapshotOk
                                                    ReconcileProgram.ReconcileEvidence.SessionCleared
                                            ))
                                            activeBinding
                                            lastSnapshot
                                            turns
                                else
                                    let! result = snapshot.GetMessages sessionId

                                    if not (isCurrent sessionId generation) then
                                        return ()
                                    else
                                        match result with
                                        | Error error ->
                                            logError "RECONCILE-SNAPSHOT" (sprintf "snapshot failed: %s" (string error))

                                            return!
                                                go
                                                    (next (ReconcileProgram.ReconcileReply.SnapshotError(string error)))
                                                    activeBinding
                                                    lastSnapshot
                                                    turns
                                        | Ok messages ->
                                            let turn = activeBinding |> Option.bind (TurnReconcile.reconcile messages)
                                            let evidence = evidenceOf turn

                                            let observedTurns =
                                                match turn with
                                                | Some value ->
                                                    Map.add
                                                        (ReconcileProgram.consumeKey (publishTurn value))
                                                        value
                                                        turns
                                                | None -> turns

                                            return!
                                                go
                                                    (next (ReconcileProgram.ReconcileReply.SnapshotOk evidence))
                                                    activeBinding
                                                    (Some messages)
                                                    observedTurns

                            | ReconcileProgram.ReconcileCommand.Delay milliseconds ->
                                do! delayMs milliseconds

                                if isCurrent sessionId generation then
                                    return!
                                        go
                                            (next ReconcileProgram.ReconcileReply.DelayDone)
                                            activeBinding
                                            lastSnapshot
                                            turns
                                else
                                    return ()

                            | ReconcileProgram.ReconcileCommand.StorePublishMaps(_, maps) ->
                                if isCurrent sessionId generation then
                                    recordMaps sessionId maps

                                    return!
                                        go
                                            (next ReconcileProgram.ReconcileReply.PublishMapsStored)
                                            activeBinding
                                            lastSnapshot
                                            turns
                                else
                                    return ()

                            | ReconcileProgram.ReconcileCommand.PublishTurn value ->
                                if isCurrent sessionId generation then
                                    let turn = Map.find (ReconcileProgram.consumeKey value) turns
                                    do! onTurn turn

                                if isCurrent sessionId generation then
                                    return!
                                        go
                                            (next ReconcileProgram.ReconcileReply.PublishDone)
                                            activeBinding
                                            lastSnapshot
                                            turns
                                else
                                    return ()

                            | ReconcileProgram.ReconcileCommand.ObserveSnapshot _ ->
                                match lastSnapshot with
                                | Some messages when isCurrent sessionId generation ->
                                    do! observeSnapshot sessionId messages
                                | None -> ()
                                | Some _ -> ()

                                if isCurrent sessionId generation then
                                    return!
                                        go
                                            (next ReconcileProgram.ReconcileReply.ObserveDone)
                                            activeBinding
                                            lastSnapshot
                                            turns
                                else
                                    return ()

                            | ReconcileProgram.ReconcileCommand.ProtocolMismatch(expected, actual) ->
                                logError "RECONCILE-PROTOCOL" (sprintf "expected %s, received %s" expected actual)

                                return!
                                    go (next ReconcileProgram.ReconcileReply.UnitOk) activeBinding lastSnapshot turns
                }

            go program None None Map.empty

        member private this.Drain(sessionId: SessionId, generation: int) : Task =
            task {
                match takeWork sessionId generation with
                | Work.Drained -> return ()
                | Work.Wake ->
                    do!
                        this.Interpret(
                            sessionId,
                            generation,
                            ReconcileProgram.materializePassWithMaps
                                (SessionId.value sessionId)
                                delays
                                budget
                                (mapsFor sessionId)
                        )

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
                    logError "RECONCILE-INTERPRETER" error.Message

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
