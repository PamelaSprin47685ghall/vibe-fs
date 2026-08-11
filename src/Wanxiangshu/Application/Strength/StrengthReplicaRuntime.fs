namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session


[<RequireQualifiedAccess>]
type StrengthReplicaTerminal =
    | BudgetReached
    | TextCompleted
    | Failed of reason: string
    | TimedOut
    | Cancelled
    | InvalidFrame of reason: string

type StrengthReplicaOutcome =
    { ReplicaSessionId: SessionId
      RequestsAdmitted: int
      Batches: StrengthRequestBatch list
      Terminal: StrengthReplicaTerminal }

type private StrengthReplicaDecisionState =
    { Owner: SessionId
      Replica: SessionId
      DecisionId: StrengthDecisionId
      Completion: TaskCompletionSource<StrengthReplicaOutcome>
      RequestsAdmitted: int
      Batches: StrengthRequestBatch list }

/// STRENGTH-003/004/009/011: physical coordinator for one decision-local leaf.
///
/// Message replacement and the physical K+1 gate belong to
/// StrengthReplicaTransform. Universal live ownership/capability truth belongs to
/// Session.StrengthRuntime. This coordinator only owns create/send/wait/cancel and
/// the in-flight completion cells required to return a decision result.
type StrengthReplicaRuntime
    (
        sessions: ISessionHostPort,
        dispatcher: PromptDispatcher.Runtime,
        timer: ITimerPort,
        liveRegistry: StrengthRuntime,
        registerReplica: SessionId -> SessionId -> string -> unit,
        ?workspaceDirectory: string,
        ?maxLatencyMs: int,
        ?maxFrameBytes: int
    ) =

    let gate = obj ()
    let byReplica = Dictionary<string, StrengthReplicaDecisionState>()
    let directory = workspaceDirectory
    let latencyMs = max 1 (defaultArg maxLatencyMs 2500)
    let frameByteLimit = max 1 (defaultArg maxFrameBytes 65536)

    let key (sessionId: SessionId) = SessionId.value sessionId

    let tryState replica =
        lock gate (fun () ->
            match byReplica.TryGetValue(key replica) with
            | true, state -> Some state
            | false, _ -> None)

    let replaceState (previous: StrengthReplicaDecisionState) (next: StrengthReplicaDecisionState) =
        lock gate (fun () ->
            match byReplica.TryGetValue(key previous.Replica) with
            | true, current when Object.ReferenceEquals(current.Completion, previous.Completion) ->
                byReplica.[key previous.Replica] <- next
                true
            | _ -> false)

    let requestsFor terminal (state: StrengthReplicaDecisionState) =
        match terminal with
        | StrengthReplicaTerminal.TextCompleted -> max state.RequestsAdmitted (min 2 (List.length state.Batches + 1))
        | _ -> state.RequestsAdmitted

    let outcome terminal (state: StrengthReplicaDecisionState) =
        { ReplicaSessionId = state.Replica
          RequestsAdmitted = requestsFor terminal state
          Batches = state.Batches
          Terminal = terminal }

    let completionWins (completion: Task<StrengthReplicaOutcome>) (deadline: IDeadlineHandle) : Task<bool> =
        let completed =
            task {
                let! _ = completion
                return true
            }

        let timedOut =
            task {
                do! deadline.Delay
                return false
            }

        emitJsExpr (completed, timedOut) "Promise.race([$0, $1])"

    let complete terminal (state: StrengthReplicaDecisionState) =
        state.Completion.TrySetResult(outcome terminal state) |> ignore

    let abortReplica (state: StrengthReplicaDecisionState) =
        task {
            try
                let! _ = sessions.AbortSession state.Replica
                return ()
            with _ ->
                return ()
        }

    let removeState (state: StrengthReplicaDecisionState) =
        lock gate (fun () ->
            match byReplica.TryGetValue(key state.Replica) with
            | true, current when Object.ReferenceEquals(current.Completion, state.Completion) ->
                byReplica.Remove(key state.Replica) |> ignore
            | _ -> ())

        liveRegistry.Retire state.Replica |> ignore

    member _.MaxFrameBytes = frameByteLimit

    member _.IsReplica(sessionId: SessionId) =
        liveRegistry.TryFindByReplica sessionId |> Option.isSome

    member _.TryOwner(sessionId: SessionId) =
        liveRegistry.TryFindByReplica sessionId
        |> Option.map (fun binding -> binding.OwnerSessionId)

    member _.TryDecision(sessionId: SessionId) =
        liveRegistry.TryFindByReplica sessionId
        |> Option.map (fun binding -> binding.DecisionId)

    /// Called after the Replica request profile has been bound by XWire, but
    /// before any ordinary Work transform writer. A Retired outcome means the
    /// transform already aborted the child, so K+1 cannot escape physically.
    member _.HandleTransform(output: obj) : Task<bool> =
        task {
            match Projection.projectionSessionIdFromMessages output with
            | None -> return false
            | Some sessionIdText ->
                let replica = SessionId.create sessionIdText

                match tryState replica with
                | None -> return false
                | Some state ->
                    let! transformed = StrengthReplicaTransform.apply HostDigest.sha256Hex liveRegistry sessions output

                    match transformed with
                    | StrengthReplicaTransformOutcome.NotReplica -> return false
                    | StrengthReplicaTransformOutcome.Ready batches ->
                        let limit =
                            liveRegistry.TryFindByReplica replica
                            |> Option.map (fun binding -> StrengthBudget.requestLimit binding.Budget)
                            |> Option.defaultValue 0

                        let next =
                            { state with
                                RequestsAdmitted = min limit (List.length batches + 1)
                                Batches = batches }

                        replaceState state next |> ignore
                        return true
                    | StrengthReplicaTransformOutcome.Retired(reason, batches) ->
                        let next =
                            { state with
                                RequestsAdmitted = List.length batches
                                Batches = batches }

                        replaceState state next |> ignore

                        if reason = "provider-request-budget-reached" then
                            complete StrengthReplicaTerminal.BudgetReached next
                        elif
                            reason.StartsWith("invalid-replica-frame", StringComparison.Ordinal)
                            || reason.StartsWith("projection-conflict", StringComparison.Ordinal)
                        then
                            complete (StrengthReplicaTerminal.InvalidFrame reason) next
                        else
                            complete (StrengthReplicaTerminal.Failed reason) next

                        return true
        }

    /// Replica terminal observations are consumed before ordinary Work reconcile.
    /// They never touch owner fallback, Companion, Review or InteractionRepair.
    member _.HandleTurn(turn: ReconciledTurn) : bool =
        match tryState turn.SessionId with
        | None -> false
        | Some state ->
            match turn.Outcome with
            | ReconcileProgram.TurnCompleted -> complete StrengthReplicaTerminal.TextCompleted state
            | ReconcileProgram.TurnFailed reason
            | ReconcileProgram.TurnAborted reason -> complete (StrengthReplicaTerminal.Failed reason) state
            | ReconcileProgram.TurnNeedsContinuation _
            | ReconcileProgram.TurnInProgress -> ()

            true

    member _.CancelOwner(owner: SessionId) : Task =
        task {
            match liveRegistry.TryFindByOwner owner with
            | None -> ()
            | Some binding ->
                match tryState binding.ReplicaSessionId with
                | None -> liveRegistry.Retire binding.ReplicaSessionId |> ignore
                | Some state ->
                    complete StrengthReplicaTerminal.Cancelled state
                    do! abortReplica state
        }

    member _.StartDecision
        (
            owner: SessionId,
            decisionId: StrengthDecisionId,
            targetProviderRun: ProviderRunIdentity,
            budget: StrengthBudget,
            fastAgent: string,
            localizedMirror: WireMessage list,
            mirrorSemanticDigest: string
        ) : Task<Result<StrengthReplicaOutcome, string>> =
        task {
            if StrengthBudget.requestLimit budget = 0 then
                return Error "StrengthReplica cannot start with K0"
            else
                match ManagedAgent.tryParse fastAgent with
                | None -> return Error(sprintf "StrengthReplica fast agent is unmanaged: %s" fastAgent)
                | Some managed when not (Set.contains managed.Role StrengthPolicy.eligibleRoles) ->
                    return Error(sprintf "StrengthReplica role is ineligible: %A" managed.Role)
                | Some managed when managed.Tier <> AgentTier.Fast ->
                    return Error(sprintf "StrengthReplica agent is not fast tier: %s" fastAgent)
                | Some managed ->
                    match liveRegistry.TryFindByOwner owner with
                    | Some _ -> return Error "StrengthReplica owner already has an active decision"
                    | None ->
                        let! created =
                            sessions.CreateChildSession(
                                owner,
                                { Title = Some fastAgent
                                  Agent = Some fastAgent
                                  Directory = directory }
                            )

                        match created with
                        | Error error -> return Error error
                        | Ok replica ->
                            let capabilities =
                                PromptAuthority.toolCapabilitiesFor managed.Role ProviderRequestKind.StrengthReplica

                            let binding: StrengthReplicaBinding =
                                { OwnerSessionId = owner
                                  ReplicaSessionId = replica
                                  DecisionId = decisionId
                                  TargetProviderRun = targetProviderRun
                                  CanonicalRole = managed.Role
                                  Budget = budget
                                  MaxFrameBytes = frameByteLimit
                                  SemanticDigest = mirrorSemanticDigest
                                  LocalizedMirrorMessages = localizedMirror
                                  ToolCapabilitySet = capabilities }

                            match liveRegistry.Register binding with
                            | Error error ->
                                let! _ = sessions.AbortSession replica
                                return Error(sprintf "StrengthReplica live registration failed: %A" error)
                            | Ok() ->
                                let state =
                                    { Owner = owner
                                      Replica = replica
                                      DecisionId = decisionId
                                      Completion = TaskCompletionSource<StrengthReplicaOutcome>()
                                      RequestsAdmitted = 0
                                      Batches = [] }

                                let collectorClaimed =
                                    lock gate (fun () ->
                                        if byReplica.ContainsKey(key replica) then
                                            false
                                        else
                                            byReplica.[key replica] <- state
                                            true)

                                if not collectorClaimed then
                                    liveRegistry.Retire replica |> ignore
                                    let! _ = sessions.AbortSession replica
                                    return Error "StrengthReplica in-flight state collided after live registration"
                                else
                                    registerReplica owner replica fastAgent

                                    try
                                        // The physical bootstrap is never provider-visible: the
                                        // Replica transform replaces it with FrozenMirror before
                                        // request #1 is admitted.
                                        match!
                                            dispatcher.SendAgentOwnerRootWithTools
                                                sessions
                                                replica
                                                "Continue."
                                                fastAgent
                                                directory
                                                PromptDispatcher.AwaitMode.Detached
                                                None
                                                StrengthReplicaTools.exactReadonlyHostToolMap
                                        with
                                        | Error error -> complete (StrengthReplicaTerminal.Failed error) state
                                        | Ok _ -> ()

                                        let deadline = timer.Delay latencyMs
                                        let completionTask = state.Completion.Task
                                        let! completed = completionWins completionTask deadline

                                        if completed then
                                            deadline.Cancel()
                                        else
                                            let current = tryState replica |> Option.defaultValue state
                                            complete StrengthReplicaTerminal.TimedOut current
                                            do! abortReplica current

                                        let! result = completionTask
                                        removeState state
                                        return Ok result
                                    with ex ->
                                        let current = tryState replica |> Option.defaultValue state
                                        complete (StrengthReplicaTerminal.Failed ex.Message) current
                                        do! abortReplica current
                                        let! result = state.Completion.Task
                                        removeState state
                                        return Ok result
        }

    member _.Dispose() =
        let states = lock gate (fun () -> byReplica.Values |> Seq.toList)

        for state in states do
            complete StrengthReplicaTerminal.Cancelled state

        lock gate (fun () -> byReplica.Clear())
        liveRegistry.Clear()
        timer.Dispose()

    interface IDisposable with
        member this.Dispose() = this.Dispose()
