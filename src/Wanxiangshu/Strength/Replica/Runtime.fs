namespace Wanxiangshu.Strength.Replica

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
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
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Composition.Turn

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

type StrengthDryRunStart =
    { ReplicaSessionId: SessionId
      Completion: Task<StrengthReplicaOutcome> }

type private StrengthReplicaDecisionState =
    { Owner: SessionId
      Replica: SessionId
      DecisionId: StrengthDecisionId
      Completion: TaskCompletionSource<StrengthReplicaOutcome>
      RequestsAdmitted: int
      Batches: StrengthRequestBatch list }

module private StrengthReplicaRuntimeLogic =

    let requireNonEmptyBudget (budget: StrengthBudget) =
        if StrengthBudget.requestLimit budget = 0 then
            Error "StrengthReplica cannot start with K0"
        else
            Ok()

    let parseEligibleFastAgent (fastAgent: string) =
        match ManagedAgent.tryParse fastAgent with
        | None -> Error(sprintf "StrengthReplica fast agent is unmanaged: %s" fastAgent)
        | Some managed when not (Set.contains managed.Role StrengthPolicy.eligibleRoles) ->
            Error(sprintf "StrengthReplica role is ineligible: %A" managed.Role)
        | Some managed when managed.Tier <> AgentTier.Fast ->
            Error(sprintf "StrengthReplica agent is not fast tier: %s" fastAgent)
        | Some managed -> Ok managed

    let requireOwnerIdle (liveRegistry: StrengthRuntime) (owner: SessionId) =
        match liveRegistry.TryFindByOwner owner with
        | Some _ -> Error "StrengthReplica owner already has an active decision"
        | None -> Ok()

    let registerLiveOrAbort
        (sessions: ISessionHostPort)
        (liveRegistry: StrengthRuntime)
        (releaseModel: (SessionId -> unit) option)
        (binding: StrengthReplicaBinding)
        (replica: SessionId)
        : Task<Result<unit, string>> =
        task {
            match liveRegistry.Register binding with
            | Error error ->
                releaseModel |> Option.iter (fun release -> release replica)
                let! _ = sessions.AbortSession replica
                return Error(sprintf "StrengthReplica live registration failed: %A" error)
            | Ok() -> return Ok()
        }

    let private safeAcquire (acquire: SessionId -> string -> OpencodeModel option) replica fastAgent =
        try
            Ok(acquire replica fastAgent)
        with ex ->
            Error ex

    let private executeAcquireModel
        (sessions: ISessionHostPort)
        (acquire: SessionId -> string -> OpencodeModel option)
        (replica: SessionId)
        (fastAgent: string)
        : Task<Result<OpencodeModel option, string>> =
        task {
            match safeAcquire acquire replica fastAgent with
            | Ok(Some model) -> return Ok(Some model)
            | Ok None ->
                let! _ = sessions.AbortSession replica
                return Error "model-capacity-unavailable"
            | Error ex ->
                let! _ = sessions.AbortSession replica
                return raise ex
        }

    let acquireOptionalModelOrAbort
        (sessions: ISessionHostPort)
        (tryAcquireModel: (SessionId -> string -> OpencodeModel option) option)
        (replica: SessionId)
        (fastAgent: string)
        : Task<Result<OpencodeModel option, string>> =
        match tryAcquireModel with
        | None -> Task.FromResult(Ok None)
        | Some acquire -> executeAcquireModel sessions acquire replica fastAgent

    let tryClaimCollector
        (gate: obj)
        (byReplica: Dictionary<string, StrengthReplicaDecisionState>)
        (sessionKey: SessionId -> string)
        (replica: SessionId)
        (state: StrengthReplicaDecisionState)
        =
        lock gate (fun () ->
            if byReplica.ContainsKey(sessionKey replica) then
                false
            else
                byReplica.[sessionKey replica] <- state
                true)

    let claimCollectorOrFail
        (gate: obj)
        (byReplica: Dictionary<string, StrengthReplicaDecisionState>)
        (sessionKey: SessionId -> string)
        (liveRegistry: StrengthRuntime)
        (sessions: ISessionHostPort)
        (releaseModel: (SessionId -> unit) option)
        (registerReplica: SessionId -> SessionId -> string -> unit)
        (owner: SessionId)
        (replica: SessionId)
        (fastAgent: string)
        (state: StrengthReplicaDecisionState)
        : Task<Result<unit, string>> =
        task {
            if tryClaimCollector gate byReplica sessionKey replica state then
                registerReplica owner replica fastAgent
                return Ok()
            else
                liveRegistry.Retire replica |> ignore
                releaseModel |> Option.iter (fun release -> release replica)
                let! _ = sessions.AbortSession replica
                return Error "StrengthReplica in-flight state collided after live registration"
        }

    let applyBootstrapSendResult
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (state: StrengthReplicaDecisionState)
        (sent: Result<'ignored, string>)
        =
        match sent with
        | Error error -> complete (StrengthReplicaTerminal.Failed error) state
        | Ok _ -> ()

    let bootstrapDetachedSend
        (dispatcher: PromptDispatcher.Runtime)
        (sessions: ISessionHostPort)
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (abortReplica: StrengthReplicaDecisionState -> Task<unit>)
        (promptModel: OpencodeModel option)
        (directory: string option)
        (replica: SessionId)
        (fastAgent: string)
        (state: StrengthReplicaDecisionState)
        : Task<unit> =
        task {
            try
                let! sent =
                    dispatcher.SendAgentOwnerRootWithTools
                        sessions
                        replica
                        "Continue."
                        fastAgent
                        directory
                        PromptDispatcher.AwaitMode.Detached
                        None
                        StrengthReplicaTools.exactReadonlyHostToolMap
                        promptModel

                applyBootstrapSendResult complete state sent
            with ex ->
                complete (StrengthReplicaTerminal.Failed ex.Message) state
                do! abortReplica state
        }

    let terminalForRetiredReason (reason: string) =
        if reason = "provider-request-budget-reached" then
            StrengthReplicaTerminal.BudgetReached
        elif
            reason.StartsWith("invalid-replica-frame", StringComparison.Ordinal)
            || reason.StartsWith("projection-conflict", StringComparison.Ordinal)
        then
            StrengthReplicaTerminal.InvalidFrame reason
        else
            StrengthReplicaTerminal.Failed reason

    let applyTransformOutcome
        (liveRegistry: StrengthRuntime)
        (replaceState: StrengthReplicaDecisionState -> StrengthReplicaDecisionState -> bool)
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (replica: SessionId)
        (state: StrengthReplicaDecisionState)
        (transformed: StrengthReplicaTransformOutcome)
        : bool =
        match transformed with
        | StrengthReplicaTransformOutcome.NotReplica -> false
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
            true
        | StrengthReplicaTransformOutcome.Retired(reason, batches) ->
            let next =
                { state with
                    RequestsAdmitted = List.length batches
                    Batches = batches }

            replaceState state next |> ignore
            complete (terminalForRetiredReason reason) next
            true

    let handleTransformSession
        (tryState: SessionId -> StrengthReplicaDecisionState option)
        (replaceState: StrengthReplicaDecisionState -> StrengthReplicaDecisionState -> bool)
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (liveRegistry: StrengthRuntime)
        (sessions: ISessionHostPort)
        (sessionIdText: string)
        (output: obj)
        : Task<bool> =
        task {
            let replica = SessionId.create sessionIdText

            match tryState replica with
            | None -> return false
            | Some state ->
                let! transformed = StrengthReplicaTransform.apply HostDigest.sha256Hex liveRegistry sessions output

                return applyTransformOutcome liveRegistry replaceState complete replica state transformed
        }

    let completeFromTurnOutcome
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (state: StrengthReplicaDecisionState)
        (outcome: ReconcileProgram.TurnOutcome)
        =
        match outcome with
        | ReconcileProgram.TurnCompleted -> complete StrengthReplicaTerminal.TextCompleted state
        | ReconcileProgram.TurnFailed reason
        | ReconcileProgram.TurnAborted reason -> complete (StrengthReplicaTerminal.Failed reason) state
        | ReconcileProgram.TurnNeedsContinuation _
        | ReconcileProgram.TurnInProgress -> ()

    let cancelReplicaBinding
        (tryState: SessionId -> StrengthReplicaDecisionState option)
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (abortReplica: StrengthReplicaDecisionState -> Task<unit>)
        (liveRegistry: StrengthRuntime)
        (releaseModel: (SessionId -> unit) option)
        (binding: StrengthReplicaBinding)
        : Task<unit> =
        task {
            match tryState binding.ReplicaSessionId with
            | None ->
                liveRegistry.Retire binding.ReplicaSessionId |> ignore
                releaseModel |> Option.iter (fun release -> release binding.ReplicaSessionId)
            | Some state ->
                complete StrengthReplicaTerminal.Cancelled state
                do! abortReplica state
        }

    let settleCompletionRace
        (completed: bool)
        (deadline: IDeadlineHandle)
        (current: StrengthReplicaDecisionState)
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (abortReplica: StrengthReplicaDecisionState -> Task<unit>)
        : Task<unit> =
        task {
            if completed then
                deadline.Cancel()
            else
                complete StrengthReplicaTerminal.TimedOut current
                do! abortReplica current
        }

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
        ?maxFrameBytes: int,
        ?tryAcquireModel: (SessionId -> string -> OpencodeModel option),
        ?releaseModel: (SessionId -> unit)
    ) =

    let gate = obj ()
    // DSL-MUTABLE: resource — replica decision state map
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
        AsyncSupport.trySetResult state.Completion (outcome terminal state) |> ignore

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
        releaseModel |> Option.iter (fun release -> release state.Replica)

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
            match ProviderWireDecode.projectionSessionIdFromMessages output with
            | None -> return false
            | Some sessionIdText ->
                return!
                    StrengthReplicaRuntimeLogic.handleTransformSession
                        tryState
                        replaceState
                        complete
                        liveRegistry
                        sessions
                        sessionIdText
                        output
        }

    /// Replica terminal observations are consumed before ordinary Work reconcile.
    /// They never touch owner fallback, Companion, Review or InteractionRepair.
    member _.HandleTurn(turn: ReconciledTurn) : bool =
        match tryState turn.SessionId with
        | None -> false
        | Some state ->
            StrengthReplicaRuntimeLogic.completeFromTurnOutcome complete state turn.Outcome
            true

    member _.CancelOwner(owner: SessionId) : Task =
        task {
            match liveRegistry.TryFindByOwner owner with
            | None -> ()
            | Some binding ->
                do!
                    StrengthReplicaRuntimeLogic.cancelReplicaBinding
                        tryState
                        complete
                        abortReplica
                        liveRegistry
                        releaseModel
                        binding
        }

    member private _.ObserveDryRun(state: StrengthReplicaDecisionState) =
        task {
            try
                let deadline = timer.Delay latencyMs
                let completionTask = state.Completion.Task
                let! completed = completionWins completionTask deadline

                do!
                    StrengthReplicaRuntimeLogic.settleCompletionRace
                        completed
                        deadline
                        (tryState state.Replica |> Option.defaultValue state)
                        complete
                        abortReplica

                let! _ = completionTask
                removeState state
            with ex ->
                let current = tryState state.Replica |> Option.defaultValue state
                complete (StrengthReplicaTerminal.Failed ex.Message) current
                do! abortReplica current
                removeState state
        }

    member this.StartDryRun
        (
            owner: SessionId,
            decisionId: StrengthDecisionId,
            targetProviderRun: ProviderRunIdentity,
            budget: StrengthBudget,
            fastAgent: string,
            localizedMirror: WireMessage list,
            mirrorSemanticDigest: string
        ) : Task<Result<StrengthDryRunStart, string>> =
        task {
            match!
                this.StartReplica(
                    owner,
                    decisionId,
                    targetProviderRun,
                    budget,
                    fastAgent,
                    localizedMirror,
                    mirrorSemanticDigest
                )
            with
            | Error error -> return Error error
            | Ok state ->
                this.ObserveDryRun state |> ignore

                return
                    Ok
                        { ReplicaSessionId = state.Replica
                          Completion = state.Completion.Task }
        }

    member private _.StartReplica
        (
            owner: SessionId,
            decisionId: StrengthDecisionId,
            targetProviderRun: ProviderRunIdentity,
            budget: StrengthBudget,
            fastAgent: string,
            localizedMirror: WireMessage list,
            mirrorSemanticDigest: string
        ) : Task<Result<StrengthReplicaDecisionState, string>> =
        taskResult {
            do! StrengthReplicaRuntimeLogic.requireNonEmptyBudget budget
            let! managed = StrengthReplicaRuntimeLogic.parseEligibleFastAgent fastAgent
            do! StrengthReplicaRuntimeLogic.requireOwnerIdle liveRegistry owner

            let! replica =
                sessions.CreateChildSession(
                    owner,
                    { Title = Some fastAgent
                      Agent = Some fastAgent
                      Directory = directory }
                )

            let! promptModel =
                StrengthReplicaRuntimeLogic.acquireOptionalModelOrAbort sessions tryAcquireModel replica fastAgent

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

            do! StrengthReplicaRuntimeLogic.registerLiveOrAbort sessions liveRegistry releaseModel binding replica

            let state =
                { Owner = owner
                  Replica = replica
                  DecisionId = decisionId
                  Completion = TaskCompletionSource<StrengthReplicaOutcome>()
                  RequestsAdmitted = 0
                  Batches = [] }

            do!
                StrengthReplicaRuntimeLogic.claimCollectorOrFail
                    gate
                    byReplica
                    key
                    liveRegistry
                    sessions
                    releaseModel
                    registerReplica
                    owner
                    replica
                    fastAgent
                    state

            do!
                StrengthReplicaRuntimeLogic.bootstrapDetachedSend
                    dispatcher
                    sessions
                    complete
                    abortReplica
                    promptModel
                    directory
                    replica
                    fastAgent
                    state
                |> TaskResultCE.ofTask

            return state
        }

    member this.StartDecision
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
            match!
                this.StartReplica(
                    owner,
                    decisionId,
                    targetProviderRun,
                    budget,
                    fastAgent,
                    localizedMirror,
                    mirrorSemanticDigest
                )
            with
            | Error error -> return Error error
            | Ok state ->
                try
                    let deadline = timer.Delay latencyMs
                    let completionTask = state.Completion.Task
                    let! completed = completionWins completionTask deadline

                    do!
                        StrengthReplicaRuntimeLogic.settleCompletionRace
                            completed
                            deadline
                            (tryState state.Replica |> Option.defaultValue state)
                            complete
                            abortReplica

                    let! result = completionTask
                    removeState state
                    return Ok result
                with ex ->
                    let current = tryState state.Replica |> Option.defaultValue state
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
            releaseModel |> Option.iter (fun release -> release state.Replica)

        lock gate (fun () -> byReplica.Clear())
        liveRegistry.Clear()
        timer.Dispose()

    interface IDisposable with
        member this.Dispose() = this.Dispose()
