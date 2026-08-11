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
open Wanxiangshu.Tools

[<RequireQualifiedAccess>]
type StrengthReplicaTerminal =
    | BudgetReached
    | TextCompleted
    | Failed of reason: string
    | TimedOut
    | Cancelled
    | InvalidFrame of reason: string

/// One decision-local physical result. Batches are only complete provider request
/// batches; Replica prose is intentionally absent.
type StrengthReplicaOutcome =
    { ReplicaSessionId: SessionId
      RequestsAdmitted: int
      Batches: StrengthRequestBatch list
      Terminal: StrengthReplicaTerminal }

type private StrengthReplicaDecisionState =
    { Owner: SessionId
      Replica: SessionId
      DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      Budget: StrengthBudget
      FastAgent: string
      FrozenMirror: WireMessage list
      MirrorSemanticDigest: string
      Completion: TaskCompletionSource<StrengthReplicaOutcome>
      mutable RequestsAdmitted: int
      mutable Batches: StrengthRequestBatch list }

/// STRENGTH-003/004/009/011: decision-local InternalLeaf runtime.
///
/// The Host's own step loop executes readonly tool calls. This runtime owns only
/// the physical child, provider-request budget gate, frozen mirror, completed
/// batch collection and cancellation. It owns no durable truth and never advances
/// the owner's fallback/review state.
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

    let tryStateByReplica replica =
        lock gate (fun () ->
            match byReplica.TryGetValue(key replica) with
            | true, state -> Some state
            | false, _ -> None)

    let outcome terminal (state: StrengthReplicaDecisionState) =
        { ReplicaSessionId = state.Replica
          RequestsAdmitted = state.RequestsAdmitted
          Batches = state.Batches
          Terminal = terminal }

    let complete terminal (state: StrengthReplicaDecisionState) =
        state.Completion.TrySetResult(outcome terminal state) |> ignore

    let removeState (state: StrengthReplicaDecisionState) =
        lock gate (fun () ->
            match byReplica.TryGetValue(key state.Replica) with
            | true, current when Object.ReferenceEquals(current.Completion, state.Completion) ->
                byReplica.Remove(key state.Replica) |> ignore
            | _ -> ())

        liveRegistry.Retire state.Replica |> ignore

    let abortReplica (state: StrengthReplicaDecisionState) =
        task {
            try
                let! _ = sessions.AbortSession state.Replica
                return ()
            with _ ->
                return ()
        }

    let snapshotFor (current: ProviderWireProjection) : ProjectionSnapshot =
        { CurrentProjection = ProviderProjection.toSemantic current
          CommittedPrefix = None
          BlogFrames = []
          TransportMessages = Set.empty
          HostReanchor = None }

    let renderReplicaView (state: StrengthReplicaDecisionState) (rawMessages: obj list) : Result<obj list, string> =
        let current = Projection.decodeMessageView rawMessages

        let mirror =
            ProjectionIntent.useStrengthMirror
                state.DecisionId
                state.TargetProviderRun
                state.MirrorSemanticDigest
                state.FrozenMirror

        let intentsResult =
            match state.Batches with
            | [] -> Ok [ mirror ]
            | batches ->
                match StrengthFrame.tryBuild HostDigest.sha256Hex frameByteLimit batches with
                | Error error -> Error(sprintf "Replica local frame invalid: %A" error)
                | Ok bundle ->
                    Ok [ mirror; ProjectionIntent.strengthReplicaLocal state.Owner state.DecisionId bundle ]

        match intentsResult with
        | Error error -> Error error
        | Ok intents ->
            match ProjectionPlanner.plan intents with
            | Error conflict -> Error(sprintf "Replica projection conflict: %A" conflict)
            | Ok _ ->
                let rendered =
                    ProjectionRenderer.renderMessagesWithHostIds
                        HostDigest.sha256Hex
                        (snapshotFor current)
                        current.Messages
                        intents

                Projection.tryApplyRenderedMessages (key state.Replica) HostDigest.sha256Hex rendered

    let refreshBatches (state: StrengthReplicaDecisionState) (rawMessages: obj list) : Result<unit, string> =
        let collected =
            Projection.decodeMessageView rawMessages
            |> fun projection -> StrengthBatchCollector.collectCompleteBatches projection.Messages

        let oldCount = List.length state.Batches

        if List.length collected < oldCount || (List.truncate oldCount collected <> state.Batches) then
            Error "Replica completed-batch history changed across transforms"
        else
            state.Batches <- collected
            Ok()

    member _.IsReplica(sessionId: SessionId) = liveRegistry.TryFindByReplica sessionId |> Option.isSome

    member _.TryOwner(sessionId: SessionId) =
        liveRegistry.TryFindByReplica sessionId |> Option.map (fun binding -> binding.OwnerSessionId)

    member _.TryDecision(sessionId: SessionId) =
        liveRegistry.TryFindByReplica sessionId |> Option.map (fun binding -> binding.DecisionId)

    /// Called at the very beginning of `chat.messages.transform` for a Replica.
    /// It harvests the previous request's complete batch, then physically stops
    /// before request K+1 by aborting while still inside the transform hook.
    member _.HandleTransform(output: obj) : Task<bool> =
        task {
            match Projection.projectionSessionIdFromMessages output with
            | None -> return false
            | Some sessionKey ->
                let replica = SessionId.create sessionKey

                match tryStateByReplica replica with
                | None -> return false
                | Some state ->
                    let rawMessages = Projection.messagesFromTransformOutput output

                    match refreshBatches state rawMessages with
                    | Error error ->
                        complete (StrengthReplicaTerminal.InvalidFrame error) state
                        do! abortReplica state
                        return true
                    | Ok() ->
                        let requestLimit = StrengthBudget.requestLimit state.Budget

                        if state.RequestsAdmitted >= requestLimit then
                            // This transform would otherwise become K+1. Abort before
                            // returning so handle.process observes the aborted state and
                            // no provider request is emitted.
                            complete StrengthReplicaTerminal.BudgetReached state
                            do! abortReplica state
                            return true
                        else
                            match renderReplicaView state rawMessages with
                            | Error error ->
                                complete (StrengthReplicaTerminal.InvalidFrame error) state
                                do! abortReplica state
                                return true
                            | Ok rewritten ->
                                output?messages <- List.toArray rewritten
                                state.RequestsAdmitted <- state.RequestsAdmitted + 1
                                return true
        }

    /// Replica turns are consumed here and never enter ordinary Work reconciliation,
    /// fallback, Companion, review/finality or InteractionRepair workflows.
    member _.HandleTurn(turn: ReconciledTurn) : bool =
        match tryStateByReplica turn.SessionId with
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
            let state =
                liveRegistry.TryFindByOwner owner
                |> Option.bind (fun binding -> tryStateByReplica binding.ReplicaSessionId)

            match state with
            | None -> ()
            | Some current ->
                complete StrengthReplicaTerminal.Cancelled current
                do! abortReplica current
        }

    member _.StartDecision
        (
            owner: SessionId,
            decisionId: StrengthDecisionId,
            targetProviderRun: ProviderRunIdentity,
            budget: StrengthBudget,
            fastAgent: string,
            frozenMirror: WireMessage list,
            mirrorSemanticDigest: string
        )
        : Task<Result<StrengthReplicaOutcome, string>> =
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
                    let ownerBusy = liveRegistry.TryFindByOwner owner |> Option.isSome

                    if ownerBusy then
                        return Error "StrengthReplica owner already has an active decision"
                    else
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
                            let state =
                                { Owner = owner
                                  Replica = replica
                                  DecisionId = decisionId
                                  TargetProviderRun = targetProviderRun
                                  Budget = budget
                                  FastAgent = fastAgent
                                  FrozenMirror = frozenMirror
                                  MirrorSemanticDigest = mirrorSemanticDigest
                                  Completion = TaskCompletionSource<StrengthReplicaOutcome>()
                                  RequestsAdmitted = 0
                                  Batches = [] }

                            let capabilities =
                                PromptAuthority.toolCapabilitiesFor managed.Role ProviderRequestKind.StrengthReplica

                            let binding =
                                { OwnerSessionId = owner
                                  ReplicaSessionId = replica
                                  DecisionId = decisionId
                                  TargetProviderRun = targetProviderRun
                                  CanonicalRole = managed.Role
                                  Budget = budget
                                  ToolCapabilitySet = capabilities }

                            match liveRegistry.Register binding with
                            | Error error ->
                                do! abortReplica state
                                return Error(sprintf "StrengthReplica live registration failed: %A" error)
                            | Ok() ->
                                let collectorClaimed =
                                    lock gate (fun () ->
                                        if byReplica.ContainsKey(key replica) then
                                            false
                                        else
                                            byReplica.[key replica] <- state
                                            true)

                                if not collectorClaimed then
                                    liveRegistry.Retire replica |> ignore
                                    do! abortReplica state
                                    return Error "StrengthReplica collector state collided after live registration"
                                else
                                    registerReplica owner replica fastAgent

                                let tools = StrengthReplicaTools.exactReadonlyHostToolMap

                                try
                                    // Mechanism-neutral bootstrap text. The Replica transform
                                    // replaces this physical child history with FrozenMirror
                                    // before the provider sees request #1.
                                    match!
                                        dispatcher.SendAgentOwnerRootWithTools
                                            sessions
                                            replica
                                            "Continue."
                                            fastAgent
                                            directory
                                            PromptDispatcher.AwaitMode.Detached
                                            None
                                            tools
                                    with
                                    | Error error ->
                                        complete (StrengthReplicaTerminal.Failed error) state
                                    | Ok _ -> ()

                                    let deadline = timer.Delay latencyMs
                                    let completionTask = state.Completion.Task
                                    let! winner = Task.WhenAny([| completionTask :> Task; deadline.Delay :> Task |])

                                    if Object.ReferenceEquals(winner, completionTask :> Task) then
                                        deadline.Cancel()
                                    else
                                        complete StrengthReplicaTerminal.TimedOut state
                                        do! abortReplica state

                                    let! result = completionTask
                                    removeState state
                                    return Ok result
                                with ex ->
                                    complete (StrengthReplicaTerminal.Failed ex.Message) state
                                    do! abortReplica state
                                    let! result = state.Completion.Task
                                    removeState state
                                    return Ok result
        }

    member _.Dispose() =
        let states = lock gate (fun () -> byReplica.Values |> Seq.toList)

        for state in states do
            complete StrengthReplicaTerminal.Cancelled state

        lock gate (fun () ->
            byReplica.Clear())

        liveRegistry.Clear()

    interface IDisposable with
        member this.Dispose() = this.Dispose()
