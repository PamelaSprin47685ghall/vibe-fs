namespace Wanxiangshu.Strength.Replica

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Mission.Review.Barrier

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
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
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
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

[<RequireQualifiedAccess>]
type private StrengthReplicaPurpose =
    | Treatment
    | DryRun

type private StrengthReplicaDecisionState =
    { Owner: SessionId
      Replica: SessionId
      DecisionId: StrengthDecisionId
      Purpose: StrengthReplicaPurpose
      SemanticTerminal: StrengthReplicaTerminal option
      Completion: TaskCompletionSource<StrengthReplicaOutcome>
      RequestsAdmitted: int
      Batches: StrengthRequestBatch list }

module private StrengthReplicaRuntimeLogic =

    let requireNonEmptyBudget (budget: StrengthBudget) =
        if StrengthBudget.requestLimit budget = 0 then
            Error "StrengthReplica cannot start with K0"
        else
            Ok()

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

    let private safeAcquire (acquire: SessionId -> string -> OpencodeModel option) replica modelRole =
        try
            Ok(acquire replica modelRole)
        with ex ->
            Error ex

    let private executeAcquireModel
        (sessions: ISessionHostPort)
        (acquire: SessionId -> string -> OpencodeModel option)
        (replica: SessionId)
        (modelRole: string)
        : Task<Result<OpencodeModel option, string>> =
        task {
            match safeAcquire acquire replica modelRole with
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
        (modelRole: string)
        : Task<Result<OpencodeModel option, string>> =
        match tryAcquireModel with
        | None -> Task.FromResult(Ok None)
        | Some acquire -> executeAcquireModel sessions acquire replica modelRole

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
        (agent: string)
        (state: StrengthReplicaDecisionState)
        : Task<Result<unit, string>> =
        task {
            if tryClaimCollector gate byReplica sessionKey replica state then
                registerReplica owner replica agent
                return Ok()
            else
                liveRegistry.Retire replica |> ignore
                releaseModel |> Option.iter (fun release -> release replica)
                let! _ = sessions.AbortSession replica
                return Error "StrengthReplica in-flight state collided after live registration"
        }

    let applyBootstrapSendResult
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (abortReplica: StrengthReplicaDecisionState -> Task<unit>)
        (state: StrengthReplicaDecisionState)
        (sent: Result<'ignored, string>)
        : Task<unit> =
        task {
            match sent with
            | Error error ->
                complete (StrengthReplicaTerminal.Failed error) state
                do! abortReplica state
            | Ok _ -> ()
        }

    let bootstrapDetachedSend
        (dispatcher: PromptDispatcher.Runtime)
        (sessions: ISessionHostPort)
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (abortReplica: StrengthReplicaDecisionState -> Task<unit>)
        (promptModel: OpencodeModel option)
        (directory: string option)
        (replica: SessionId)
        (identitySeed: PromptAuthority.IdentitySeed)
        (state: StrengthReplicaDecisionState)
        : Task<unit> =
        task {
            try
                let! sent =
                    dispatcher.SendAgentOwnerRootWithTools
                        sessions
                        replica
                        (LlmFacing.renderInstruction "Continue.")
                        identitySeed
                        directory
                        PromptDispatcher.AwaitMode.Detached
                        None
                        StrengthReplicaTools.exactReadonlyHostToolMap
                        promptModel

                do! applyBootstrapSendResult complete abortReplica state sent
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
            | Some state when state.SemanticTerminal |> Option.isSome ->
                // Semantic completion may precede physical Host terminal. Keep
                // the Replica branch closed over this already-cancelled tail;
                // the original abort owns physical interruption, so do not emit
                // another abort or reinterpret the request as Ordinary work.
                return true
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

    let isReplicaPhysicalTerminal =
        function
        | ReconcileProgram.TurnCompleted
        | ReconcileProgram.TurnFailed _
        | ReconcileProgram.TurnAborted _ -> true
        | ReconcileProgram.TurnNeedsContinuation _
        | ReconcileProgram.TurnInProgress -> false

    let cancelReplicaBinding
        (tryState: SessionId -> StrengthReplicaDecisionState option)
        (tryComplete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> bool)
        (abortReplica: StrengthReplicaDecisionState -> Task<unit>)
        (liveRegistry: StrengthRuntime)
        (releaseModel: (SessionId -> unit) option)
        (binding: StrengthReplicaBinding)
        : Task<unit> =
        let cancelOpenState state =
            task {
                if tryComplete StrengthReplicaTerminal.Cancelled state then
                    do! abortReplica state
            }

        task {
            match tryState binding.ReplicaSessionId with
            | None ->
                liveRegistry.Retire binding.ReplicaSessionId |> ignore
                releaseModel |> Option.iter (fun release -> release binding.ReplicaSessionId)
            | Some state -> do! cancelOpenState state
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
        liveRegistry: StrengthRuntime,
        registerReplica: SessionId -> SessionId -> string -> unit,
        ?workspaceDirectory: string,
        ?maxFrameBytes: int,
        ?tryAcquireModel: (SessionId -> string -> OpencodeModel option),
        ?releaseModel: (SessionId -> unit)
    ) =

    let gate = obj ()
    // DSL-MUTABLE: resource — replica decision state map
    let byReplica = Dictionary<string, StrengthReplicaDecisionState>()
    let directory = workspaceDirectory
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
                byReplica.[key previous.Replica] <-
                    { next with
                        SemanticTerminal = current.SemanticTerminal }

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

    let terminalTransition terminal (state: StrengthReplicaDecisionState) =
        match state.SemanticTerminal with
        | Some _ -> None
        | None ->
            Some
                { state with
                    SemanticTerminal = Some terminal }

    let tryComplete terminal (state: StrengthReplicaDecisionState) =
        let completedState =
            lock gate (fun () ->
                match byReplica.TryGetValue(key state.Replica) with
                | true, current when Object.ReferenceEquals(current.Completion, state.Completion) ->
                    terminalTransition terminal current
                    |> Option.map (fun next ->
                        byReplica.[key state.Replica] <- next
                        next)
                | _ -> None)

        match completedState with
        | Some completed -> AsyncSupport.trySetResult completed.Completion (outcome terminal completed)
        | None -> false

    let complete terminal state = tryComplete terminal state |> ignore

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

    let cancelOpenState (state: StrengthReplicaDecisionState) =
        task {
            if tryComplete StrengthReplicaTerminal.Cancelled state then
                do! abortReplica state
        }

    let dryRunStateAtTargetTerminal (turn: ReconciledTurn) =
        liveRegistry.TryFindByOwner turn.SessionId
        |> Option.filter (fun binding -> binding.TargetProviderRun = turn.ProviderRun)
        |> Option.bind (fun binding -> tryState binding.ReplicaSessionId)
        |> Option.filter (fun state -> state.Purpose = StrengthReplicaPurpose.DryRun)

    let observeReplicaTurn state outcome =
        StrengthReplicaRuntimeLogic.completeFromTurnOutcome complete state outcome

        if StrengthReplicaRuntimeLogic.isReplicaPhysicalTerminal outcome then
            removeState state

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
            observeReplicaTurn state turn.Outcome
            true

    member _.HandleSessionDeleted(sessionId: SessionId) =
        match tryState sessionId with
        | Some state -> removeState state
        | None -> ()

    member _.CancelOwner(owner: SessionId) : Task =
        task {
            match liveRegistry.TryFindByOwner owner with
            | None -> ()
            | Some binding ->
                do!
                    StrengthReplicaRuntimeLogic.cancelReplicaBinding
                        tryState
                        tryComplete
                        abortReplica
                        liveRegistry
                        releaseModel
                        binding
        }

    /// SPEC-INV-013: DryRun is observation-only. If its own K gate/terminal has
    /// not already closed it by the time the exact owner target run terminates,
    /// that causal owner terminal is the remaining reason to stop the leaf.
    /// No elapsed-time arbitration participates in this decision.
    member _.CloseDryRunAtTargetTerminal(turn: ReconciledTurn) : Task =
        task {
            match dryRunStateAtTargetTerminal turn with
            | Some state -> do! cancelOpenState state
            | None -> ()
        }

    member private _.ObserveDryRun(state: StrengthReplicaDecisionState) =
        task {
            try
                let! _ = state.Completion.Task
                return ()
            with ex ->
                let current = tryState state.Replica |> Option.defaultValue state
                complete (StrengthReplicaTerminal.Failed ex.Message) current
                do! abortReplica current
        }

    member this.StartDryRun
        (
            owner: SessionId,
            decisionId: StrengthDecisionId,
            targetProviderRun: ProviderRunIdentity,
            budget: StrengthBudget,
            replicaAgent: string,
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
                    replicaAgent,
                    localizedMirror,
                    mirrorSemanticDigest,
                    StrengthReplicaPurpose.DryRun
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
            replicaAgent: string,
            localizedMirror: WireMessage list,
            mirrorSemanticDigest: string,
            purpose: StrengthReplicaPurpose
        ) : Task<Result<StrengthReplicaDecisionState, string>> =
        taskResult {
            do! StrengthReplicaRuntimeLogic.requireNonEmptyBudget budget
            do! StrengthReplicaRuntimeLogic.requireOwnerIdle liveRegistry owner

            let! ownerProfile =
                match (dispatcher.ProjectionFor owner).ActiveLogicalRun with
                | Some profile -> Ok profile
                | None -> Error "StrengthReplica owner has no active authority profile"

            do!
                if replicaAgent = Roles.roleLabel ownerProfile.CanonicalRole then
                    Ok()
                else
                    Error(sprintf "StrengthReplica agent '%s' disagrees with owner role" replicaAgent)

            if not (Set.contains ownerProfile.CanonicalRole StrengthPolicy.eligibleRoles) then
                return! Error(sprintf "StrengthReplica role is ineligible: %A" ownerProfile.CanonicalRole)

            // The replica simulates the owner role read-only (validated above);
            // only model routing uses the dedicated predictor label for a cheap model.
            let! identitySeed =
                PromptAuthority.issueInheritedIdentitySeed replicaAgent ownerProfile
                |> Result.mapError (sprintf "StrengthReplica identity seed is invalid: %A")

            let! replica =
                sessions.CreateChildSession(
                    owner,
                    { Title = Some replicaAgent
                      Agent = Some replicaAgent
                      Directory = directory }
                )

            let! promptModel =
                StrengthReplicaRuntimeLogic.acquireOptionalModelOrAbort sessions tryAcquireModel replica "predictor"

            let capabilities =
                PromptAuthority.toolCapabilitiesFor ownerProfile.CanonicalRole ProviderRequestKind.StrengthReplica

            let binding: StrengthReplicaBinding =
                { OwnerSessionId = owner
                  ReplicaSessionId = replica
                  DecisionId = decisionId
                  TargetProviderRun = targetProviderRun
                  CanonicalRole = ownerProfile.CanonicalRole
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
                  Purpose = purpose
                  SemanticTerminal = None
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
                    replicaAgent
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
                    identitySeed
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
            replicaAgent: string,
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
                    replicaAgent,
                    localizedMirror,
                    mirrorSemanticDigest,
                    StrengthReplicaPurpose.Treatment
                )
            with
            | Error error -> return Error error
            | Ok state ->
                try
                    let! result = state.Completion.Task
                    return Ok result
                with ex ->
                    let current = tryState state.Replica |> Option.defaultValue state
                    complete (StrengthReplicaTerminal.Failed ex.Message) current
                    do! abortReplica current
                    let! result = state.Completion.Task
                    return Ok result
        }

    member _.Dispose() =
        let states = lock gate (fun () -> byReplica.Values |> Seq.toList)

        for state in states do
            complete StrengthReplicaTerminal.Cancelled state
            releaseModel |> Option.iter (fun release -> release state.Replica)

        lock gate (fun () -> byReplica.Clear())
        liveRegistry.Clear()

    interface IDisposable with
        member this.Dispose() = this.Dispose()
