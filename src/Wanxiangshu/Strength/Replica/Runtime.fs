namespace Wanxiangshu.Strength.Replica

open Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Foundation.Identity

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

/// DELEGATE-5.3: the identity of one real outbound Replica provider request.
///
/// `PriorProviderRun` is the Host identity of the LATEST assistant response
/// already visible in the transform view, i.e. the previous real provider run of
/// this replica (None before the first response). It is derived from Host
/// message identities, never from wall clock, message counts, per-callback
/// UUIDs or the session id alone: a repeated transform for the same physical
/// outbound request sees the same view and therefore the same key, while a new
/// outbound request appends a new assistant response and therefore yields a new
/// key.
type StrengthReplicaRequestKey =
    { ReplicaSessionId: SessionId
      PriorProviderRun: ProviderRunIdentity option }

/// DELEGATE-6.2: the prepared-stage handle. The empty replica child exists and
/// carries internal identity, but no prompt was sent and no model capacity was
/// reserved. The caller persists DelegationBound before sending the prompt.
type StrengthReplicaPreparation =
    { ReplicaSessionId: SessionId
      Completion: Task<StrengthReplicaOutcome> }

/// Read-only peek at live decision state: admitted request count, completed
/// batches and the first (immutable) semantic terminal when published.
type StrengthReplicaPeek =
    { RequestsAdmitted: int
      Batches: StrengthRequestBatch list
      SemanticTerminal: StrengthReplicaTerminal option }

/// speculative-investigation-011 / R15: Partition between immutable business outcome vs physical-tail cleanup.
/// Business consumers await and observe only immutable outcome (RequestsAdmitted, Batches, Terminal),
/// while the physical session capability (retention in byReplica & liveRegistry) is held exclusively
/// for host cleanup (turn drain, aborting trailing frames, model release) until isReplicaPhysicalTerminal.
/// Presence in the physical registry never re-authorizes business execution or restarted work.
/// Index consistency law:
/// 1. Registration order: liveRegistry.Register -> claimCollectorOrFail (byReplica insertion).
/// 2. Retirement order: semantic terminal completes TaskCompletionSource -> physical terminal removes from byReplica and liveRegistry.Retire.

type private StrengthReplicaDecisionState =
    {
        Owner: SessionId
        Replica: SessionId
        DecisionId: StrengthDecisionId
        Agent: string
        /// DELEGATE-5.3: keys of the outbound requests this run really admitted.
        /// Survives Host truncation/compaction because it is owned here.
        AdmittedRequests: Set<StrengthReplicaRequestKey>
        /// Set in the prepared stage (SendPreparedPrompt consumes it); None for
        /// an AttachLiveDecision path whose caller already bootstrapped.
        IdentitySeed: PromptAuthority.IdentitySeed option
        SemanticTerminal: StrengthReplicaTerminal option
        Completion: TaskCompletionSource<StrengthReplicaOutcome>
        RequestsAdmitted: int
        Batches: StrengthRequestBatch list
    }

module private StrengthReplicaRuntimeLogic =

    let requireNonEmptyBudget (requestedRounds: ReadonlyRoundBudget) =
        if ReadonlyRoundBudget.value requestedRounds = 0 then
            Error "StrengthReplica cannot start with a zero-round budget"
        else
            Ok()

    let requireOwnerIdle (liveRegistry: StrengthRuntime) (owner: SessionId) =
        match liveRegistry.TryFindByOwner owner with
        | Some _ -> Error "StrengthReplica owner already has an active decision"
        | None -> Ok()

    let createLiveDecisionState
        (binding: StrengthReplicaBinding)
        (agent: string)
        (identitySeed: PromptAuthority.IdentitySeed option)
        =
        { Owner = binding.OwnerSessionId
          Replica = binding.ReplicaSessionId
          DecisionId = binding.DecisionId
          Agent = agent
          AdmittedRequests = Set.empty
          IdentitySeed = identitySeed
          SemanticTerminal = None
          Completion = TaskCompletionSource<StrengthReplicaOutcome>()
          RequestsAdmitted = 0
          Batches = [] }

    let registerLiveOrAbort
        (sessions: ISessionHostPort)
        (liveRegistry: StrengthRuntime)
        (releaseModel: (SessionId -> unit) option)
        (noteCleanup: SessionId -> bool -> unit)
        (binding: StrengthReplicaBinding)
        (replica: SessionId)
        : Task<Result<unit, string>> =
        task {
            match liveRegistry.Register binding with
            | Error error ->
                releaseModel |> Option.iter (fun release -> release replica)
                let! _ = sessions.AbortSession replica
                // The child exists and is terminated here, so the physical tail
                // is cleaned up even though no decision ever ran.
                noteCleanup replica true
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
        (noteCleanup: SessionId -> bool -> unit)
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
                noteCleanup replica true
                return Error "StrengthReplica in-flight state collided after live registration"
        }

    /// Claims the in-flight completion cell after live registration. Success hands
    /// the caller the immutable semantic-terminal task; collision retires the live
    /// binding and releases the model lease exactly as the inline path did. The
    /// decision state starts with an empty admission book; the caller of
    /// SendPreparedPrompt records the bootstrap admission before the first send.
    let claimLiveDecisionCell
        (gate: obj)
        (byReplica: Dictionary<string, StrengthReplicaDecisionState>)
        (sessionKey: SessionId -> string)
        (registerReplica: SessionId -> SessionId -> string -> unit)
        (liveRegistry: StrengthRuntime)
        (releaseModel: (SessionId -> unit) option)
        (noteCleanup: SessionId -> bool -> unit)
        (binding: StrengthReplicaBinding)
        (agent: string)
        (identitySeed: PromptAuthority.IdentitySeed option)
        : Result<Task<StrengthReplicaOutcome>, string> =
        let state = createLiveDecisionState binding agent identitySeed

        if tryClaimCollector gate byReplica sessionKey binding.ReplicaSessionId state then
            registerReplica binding.OwnerSessionId binding.ReplicaSessionId (Roles.roleLabel binding.CanonicalRole)

            Ok state.Completion.Task
        else
            liveRegistry.Retire binding.ReplicaSessionId |> ignore
            releaseModel |> Option.iter (fun release -> release binding.ReplicaSessionId)
            noteCleanup binding.ReplicaSessionId true
            Error "StrengthReplica in-flight state collided after live registration"

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
        (directory: string option)
        (replica: SessionId)
        (identitySeed: PromptAuthority.IdentitySeed)
        (state: StrengthReplicaDecisionState)
        : Task<unit> =
        task {
            try
                let! sent =
                    dispatcher.SendAgentOwnerRootWithTools
                        (DispatchSessionPort.ofSessionPort sessions)
                        replica
                        "Continue."
                        identitySeed
                        directory
                        PromptDispatcher.AwaitMode.Detached
                        None
                        StrengthReplicaTools.exactReadonlyHostToolMap

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

    /// DELEGATE-5.3: the exact request key of one outbound provider request,
    /// resolved from the Host transform view. `priorRun` is the last assistant
    /// ProviderRunIdentity already in the view (None before the first response),
    /// which is a real Host physical boundary, not a derived guess.
    let requestKeyOf (replica: SessionId) (rawMessages: obj list) : StrengthReplicaRequestKey =
        let priorRun =
            rawMessages
            |> List.choose (fun raw ->
                match ProviderWireCapture.decodeCapturedMessage raw with
                | Some message -> message.ProviderRun
                | None -> None)
            |> List.tryLast

        { ReplicaSessionId = replica
          PriorProviderRun = priorRun }

    /// The bootstrap request has no prior assistant response yet.
    let bootstrapRequestKey (replica: SessionId) : StrengthReplicaRequestKey =
        { ReplicaSessionId = replica
          PriorProviderRun = None }

    let private isNonToolPart =
        function
        | ProviderProjection.WireToolCall _
        | ProviderProjection.WireToolResult _ -> false
        | _ -> true

    let private isAssistantPlainText (raw: obj) =
        match ProviderWireCapture.decodeMessage raw with
        | Some message when String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) ->
            message.Parts |> List.forall isNonToolPart
        | _ -> false

    /// DELEGATE-003/005: a pure-text assistant response is the companion's own
    /// early end. It is an early-end signal, never material for the master, and
    /// the round it spent was already counted at admission.
    let private endsWithPlainText (rawMessages: obj list) : bool =
        match List.tryLast rawMessages with
        | None -> false
        | Some raw -> isAssistantPlainText raw

    [<RequireQualifiedAccess>]
    type ReplicaAdmission =
        /// The request is new for this run; the live registry owns its budget
        /// verdict and the caller consumes that verdict before any send.
        | Admitted of StrengthReplicaDecisionState
        /// The same exact request was already admitted (a repeated transform or
        /// terminal notification for the same physical request); no recount.
        | Idempotent of StrengthReplicaDecisionState
        /// A semantic terminal already closed this decision.
        | Rejected

    /// DELEGATE-5.3: request-key bookkeeping over real outbound requests. The
    /// request budget itself is owned solely by the live registry (the single
    /// account); this state only remembers which request keys this run already
    /// admitted, so a repeated transform of the same physical request never
    /// consumes a second round. Pure: the caller performs the state replacement
    /// under the decision lock.
    let admitRequest (state: StrengthReplicaDecisionState) (key: StrengthReplicaRequestKey) : ReplicaAdmission =
        if state.SemanticTerminal |> Option.isSome then
            ReplicaAdmission.Rejected
        elif Set.contains key state.AdmittedRequests then
            ReplicaAdmission.Idempotent state
        else
            ReplicaAdmission.Admitted
                { state with
                    RequestsAdmitted = state.RequestsAdmitted + 1
                    AdmittedRequests = Set.add key state.AdmittedRequests }

    let private isPrefixOf (prefix: StrengthRequestBatch list) (whole: StrengthRequestBatch list) =
        List.length prefix <= List.length whole
        && (whole |> List.truncate (List.length prefix)) = prefix

    /// DELEGATE-5.4: fold a freshly observed complete-batch view into the
    /// authoritative material this run owns. Equal view is a repeated
    /// observation (no-op, and never a recount). A longer view appends only the
    /// new tail. A view shortened by Host truncation/compaction keeps the owned
    /// material instead of rebuilding a smaller history. A diverging view is an
    /// invariant error.
    let observeCompletedBatch
        (observed: StrengthRequestBatch list)
        (current: StrengthRequestBatch list)
        : Result<StrengthRequestBatch list, string> =
        if current = observed then
            Ok observed
        elif isPrefixOf observed current then
            Ok current
        elif isPrefixOf current observed then
            Ok observed
        else
            Error "StrengthReplica observed batches diverge from the owned material"

    let private applyReadyOutcome replaceState complete state batches =
        match observeCompletedBatch state.Batches batches with
        | Ok observed ->
            replaceState state { state with Batches = observed } |> ignore
            true
        | Error reason ->
            complete (StrengthReplicaTerminal.InvalidFrame reason) state
            true

    let private applyRetiredOutcome replaceState complete state reason batches =
        match observeCompletedBatch state.Batches batches with
        | Ok observed ->
            let next = { state with Batches = observed }

            replaceState state next |> ignore
            complete (terminalForRetiredReason reason) next
            true
        | Error reason ->
            complete (StrengthReplicaTerminal.InvalidFrame reason) state
            true

    /// Applies the admitted transform to decision state. Ready fresh material is
    /// appended without touching the request count; a diverging observation is an
    /// invariant failure that closes the decision InvalidFrame. Retired
    /// transforms (budget gate, mapping refusal) publish their terminal.
    let applyTransformOutcome
        (replaceState: StrengthReplicaDecisionState -> StrengthReplicaDecisionState -> bool)
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (state: StrengthReplicaDecisionState)
        (transformed: StrengthReplicaTransformOutcome)
        : bool =
        match transformed with
        | StrengthReplicaTransformOutcome.NotReplica -> false
        | StrengthReplicaTransformOutcome.Ready batches -> applyReadyOutcome replaceState complete state batches
        | StrengthReplicaTransformOutcome.Retired(reason, batches) ->
            applyRetiredOutcome replaceState complete state reason batches

    let private resolveAdmittedState state candidate transformed =
        match transformed with
        | StrengthReplicaTransformOutcome.Retired("provider-request-budget-reached", _) -> state
        | _ -> candidate

    let private completeIfPlainTextEnded tryState complete replica handled endedInPlainText =
        let shouldComplete = handled && endedInPlainText

        match shouldComplete, tryState replica with
        | true, Some current when current.SemanticTerminal |> Option.isNone ->
            complete StrengthReplicaTerminal.TextCompleted current
        | _ -> ()

    let private handleAdmittedSession
        (tryState: SessionId -> StrengthReplicaDecisionState option)
        (replaceState: StrengthReplicaDecisionState -> StrengthReplicaDecisionState -> bool)
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (liveRegistry: StrengthRuntime)
        (sessions: ISessionHostPort)
        (replica: SessionId)
        (output: obj)
        (state: StrengthReplicaDecisionState)
        (candidate: StrengthReplicaDecisionState)
        (endedInPlainText: bool)
        : Task<bool> =
        task {
            let! transformed = StrengthReplicaTransform.apply HostDigest.sha256Hex liveRegistry sessions output true

            let admitted = resolveAdmittedState state candidate transformed
            let handled = applyTransformOutcome replaceState complete admitted transformed
            completeIfPlainTextEnded tryState complete replica handled endedInPlainText
            return handled
        }

    let private handleIdempotentSession
        (replaceState: StrengthReplicaDecisionState -> StrengthReplicaDecisionState -> bool)
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (liveRegistry: StrengthRuntime)
        (sessions: ISessionHostPort)
        (output: obj)
        (admitted: StrengthReplicaDecisionState)
        : Task<bool> =
        task {
            let! transformed = StrengthReplicaTransform.apply HostDigest.sha256Hex liveRegistry sessions output false

            return applyTransformOutcome replaceState complete admitted transformed
        }

    let private handleAdmission
        (tryState: SessionId -> StrengthReplicaDecisionState option)
        (replaceState: StrengthReplicaDecisionState -> StrengthReplicaDecisionState -> bool)
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (liveRegistry: StrengthRuntime)
        (sessions: ISessionHostPort)
        (replica: SessionId)
        (output: obj)
        (state: StrengthReplicaDecisionState)
        (key: StrengthReplicaRequestKey)
        (endedInPlainText: bool)
        : Task<bool> =
        match admitRequest state key with
        | ReplicaAdmission.Rejected -> Task.FromResult true
        | ReplicaAdmission.Admitted candidate ->
            handleAdmittedSession
                tryState
                replaceState
                complete
                liveRegistry
                sessions
                replica
                output
                state
                candidate
                endedInPlainText
        | ReplicaAdmission.Idempotent admitted ->
            handleIdempotentSession replaceState complete liveRegistry sessions output admitted

    /// DELEGATE-5.3: admission happens here, before the transform may let this
    /// outbound request leave the process. The transform only mirrors an
    /// admitted request; a request that must not be sent retires at the
    /// transform boundary (aborting before any physical N+1 provider send).
    let handleTransformSession
        (tryState: SessionId -> StrengthReplicaDecisionState option)
        (replaceState: StrengthReplicaDecisionState -> StrengthReplicaDecisionState -> bool)
        (complete: StrengthReplicaTerminal -> StrengthReplicaDecisionState -> unit)
        (liveRegistry: StrengthRuntime)
        (sessions: ISessionHostPort)
        (sessionIdText: string)
        (output: obj)
        : Task<bool> =
        let replica = SessionId.create sessionIdText

        match tryState replica with
        | None -> Task.FromResult false
        | Some state when state.SemanticTerminal |> Option.isSome -> Task.FromResult true
        | Some state ->
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput output
            let key = requestKeyOf replica rawMessages
            let endedInPlainText = endsWithPlainText rawMessages

            handleAdmission
                tryState
                replaceState
                complete
                liveRegistry
                sessions
                replica
                output
                state
                key
                endedInPlainText

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
        (removeState: StrengthReplicaDecisionState -> unit)
        (noteCleanup: SessionId -> bool -> unit)
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
                // The binding never became a decision inside this process.
                noteCleanup binding.ReplicaSessionId true
            | Some state ->
                do! cancelOpenState state
                // DELEGATE-011: an owner cancellation releases the replica and
                // its capacity fence here; it does not wait for the host tail,
                // which may never arrive for an aborted child.
                removeState state
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
        ?tryAcquireModel: (SessionId -> string -> OpencodeModel option),
        ?releaseModel: (SessionId -> unit)
    ) =

    let gate = obj ()
    // DSL-MUTABLE: resource — replica decision state map
    let byReplica = Dictionary<string, StrengthReplicaDecisionState>()
    let directory = workspaceDirectory

    let key (sessionId: SessionId) = SessionId.value sessionId

    // DSL-MUTABLE: resource — cumulative physical-tail cleanup ledger
    // (DELEGATE-011). `releasedIds` holds every replica whose physical tail was
    // really cleaned up (lease released, live binding retired); `abortedIds` is
    // the subset whose end was not the decision's own material terminal. Both
    // are keyed by identity, so a repeated observation of the same tail
    // registers nothing new: the exposed arrays accumulate, they never drain.
    // DSL-MUTABLE: resource — cumulative physical-tail cleanup ledger
    let mutable releasedIds = Set.empty
    let mutable abortedIds = Set.empty

    /// Record one real physical-tail cleanup. `aborted` marks an end the
    /// decision did not choose for itself: still live when deleted, cancelled or
    /// disposed, a provider failure, an invalid frame, or a binding that never
    /// became a decision at all.
    let notePhysicalCleanup (replica: SessionId) (aborted: bool) =
        let id = key replica

        lock gate (fun () ->
            releasedIds <- Set.add id releasedIds

            if aborted then
                abortedIds <- Set.add id abortedIds)

    /// A decision ends normally only through its own material terminal. A
    /// missing terminal means the physical tail arrived before any semantic end
    /// (or the decision never started), which is an abnormal end.
    let isMaterialEnd (terminal: StrengthReplicaTerminal option) =
        match terminal with
        | Some StrengthReplicaTerminal.TextCompleted
        | Some StrengthReplicaTerminal.BudgetReached -> true
        | Some(StrengthReplicaTerminal.Failed _)
        | Some StrengthReplicaTerminal.Cancelled
        | Some(StrengthReplicaTerminal.InvalidFrame _)
        | None -> false

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

    // DELEGATE-5.3: RequestsAdmitted is the real admission count owned by this
    // run; it is never recomputed from the visible batch list, so Host
    // truncation/compaction cannot shrink it after the fact.
    let outcome terminal (state: StrengthReplicaDecisionState) =
        { ReplicaSessionId = state.Replica
          RequestsAdmitted = state.RequestsAdmitted
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

    /// Physical-tail cleanup: removes the replica from byReplica and liveRegistry.
    /// Invoked only when the host physical turn terminal (TurnCompleted, TurnFailed, TurnAborted)
    /// is observed, or on explicit session deletion / runtime dispose. Semantic completion
    /// publishes the immutable outcome first, but retains this cleanup capability so trailing
    /// frames cannot escape as ordinary work.
    let removeState (state: StrengthReplicaDecisionState) =
        // The authoritative terminal is read before this cleanup removes the
        // entry: it decides whether the physical tail closed a material end or
        // an interrupted one.
        let terminal =
            lock gate (fun () ->
                match byReplica.TryGetValue(key state.Replica) with
                | true, current -> current.SemanticTerminal
                | _ -> state.SemanticTerminal)

        // One cleanup: the model lease is released only when this call actually
        // removed local decision state or retired a live binding, so repeated
        // physical-tail observations (turn terminal, then deletion, then dispose)
        // never double-release.
        let removedLocal =
            lock gate (fun () ->
                match byReplica.TryGetValue(key state.Replica) with
                | true, current when Object.ReferenceEquals(current.Completion, state.Completion) ->
                    byReplica.Remove(key state.Replica) |> ignore
                    true
                | _ -> false)

        let retiredLive = liveRegistry.Retire state.Replica |> Option.isSome

        if removedLocal || retiredLive then
            releaseModel |> Option.iter (fun release -> release state.Replica)
            notePhysicalCleanup state.Replica (not (isMaterialEnd terminal))

    let releaseLease sessionId =
        releaseModel |> Option.iter (fun release -> release sessionId)

    /// Orphaned live binding without local decision state (e.g. the transform
    /// retired semantic admission while the physical identity stayed live):
    /// still retire the binding and release the model lease so no orphan
    /// binding or lease survives the session.
    let retireOrphanLiveBinding sessionId =
        let retired = liveRegistry.Retire sessionId |> Option.isSome

        if retired then
            releaseLease sessionId
            // No decision state ever existed for this binding, so its physical
            // end is an interrupted one.
            notePhysicalCleanup sessionId true

    /// Owner deletion orphans its live replica: retire that side too.
    /// removeState keeps the release single-shot when decision state
    /// is still present.
    let retireReplicaSide (binding: StrengthReplicaBinding) =
        match tryState binding.ReplicaSessionId with
        | Some replicaState -> removeState replicaState
        | None ->
            liveRegistry.Retire binding.ReplicaSessionId |> ignore
            releaseLease binding.ReplicaSessionId
            // The owner went away before this side ever became a decision.
            notePhysicalCleanup binding.ReplicaSessionId true

    let retireOwnerOrphan sessionId =
        match liveRegistry.TryFindByOwner sessionId with
        | Some binding -> retireReplicaSide binding
        | None -> ()

    let clearOrphanSession sessionId =
        retireOrphanLiveBinding sessionId
        retireOwnerOrphan sessionId

    let observeReplicaTurn state outcome =
        StrengthReplicaRuntimeLogic.completeFromTurnOutcome complete state outcome

        if StrengthReplicaRuntimeLogic.isReplicaPhysicalTerminal outcome then
            removeState state

    let sendPreparedPrompt state identitySeed admitted replicaSessionId =
        task {
            replaceState state admitted |> ignore

            do!
                StrengthReplicaRuntimeLogic.bootstrapDetachedSend
                    dispatcher
                    sessions
                    complete
                    abortReplica
                    directory
                    replicaSessionId
                    identitySeed
                    admitted

            return Ok()
        }

    let acquireAndSendBootstrapPrompt state identitySeed admitted replicaSessionId =
        task {
            match!
                StrengthReplicaRuntimeLogic.acquireOptionalModelOrAbort
                    sessions
                    tryAcquireModel
                    replicaSessionId
                    state.Agent
            with
            | Error error ->
                complete (StrengthReplicaTerminal.Failed error) admitted
                do! abortReplica admitted
                return Error error
            | Ok _ ->
                return! sendPreparedPrompt state identitySeed admitted replicaSessionId
        }

    let sendPreparedPromptAdmitted state identitySeed admitted replicaSessionId =
        task {
            if not (liveRegistry.TryAdmitRequest replicaSessionId) then
                complete StrengthReplicaTerminal.BudgetReached state
                do! abortReplica state
                return Error "StrengthReplica budget reached before the bootstrap request"
            else
                return! acquireAndSendBootstrapPrompt state identitySeed admitted replicaSessionId
        }

    let sendPreparedPromptWithSeed state identitySeed replicaSessionId =
        match
            StrengthReplicaRuntimeLogic.admitRequest
                state
                (StrengthReplicaRuntimeLogic.bootstrapRequestKey replicaSessionId)
        with
        | StrengthReplicaRuntimeLogic.ReplicaAdmission.Rejected ->
            Task.FromResult(Error "StrengthReplica decision already closed")
        | StrengthReplicaRuntimeLogic.ReplicaAdmission.Idempotent _ -> Task.FromResult(Ok())
        | StrengthReplicaRuntimeLogic.ReplicaAdmission.Admitted admitted ->
            sendPreparedPromptAdmitted state identitySeed admitted replicaSessionId

    let sendPreparedPromptForState state replicaSessionId =
        match state.IdentitySeed with
        | None -> Task.FromResult(Error "StrengthReplica prepared session has no identity seed")
        | Some identitySeed -> sendPreparedPromptWithSeed state identitySeed replicaSessionId

    member _.IsReplica(sessionId: SessionId) =
        liveRegistry.TryFindByReplica sessionId |> Option.isSome

    member _.TryOwner(sessionId: SessionId) =
        liveRegistry.TryFindByReplica sessionId
        |> Option.map (fun binding -> binding.OwnerSessionId)

    member _.TryDecision(sessionId: SessionId) =
        liveRegistry.TryFindByReplica sessionId
        |> Option.map (fun binding -> binding.DecisionId)

    /// Read-only peek at one live decision. Returns None once physical-tail
    /// cleanup (turn terminal, deletion, dispose) has retired the decision.
    member _.TryPeek(replicaSessionId: SessionId) : StrengthReplicaPeek option =
        tryState replicaSessionId
        |> Option.map (fun state ->
            { RequestsAdmitted = state.RequestsAdmitted
              Batches = state.Batches
              SemanticTerminal = state.SemanticTerminal })

    /// Attach an already-live replica decision to this coordinator without
    /// Host bootstrap (child session created, model acquired, bootstrap sent
    /// by the caller). Registration order mirrors StartReplica: the live
    /// registry claims first, then the in-flight completion cell; failure
    /// retires the live binding and releases the model lease. The returned
    /// task resolves with the first (immutable) semantic terminal outcome.
    member _.AttachLiveDecision(binding: StrengthReplicaBinding) : Result<Task<StrengthReplicaOutcome>, string> =
        result {
            do! StrengthReplicaRuntimeLogic.requireNonEmptyBudget binding.RequestedRounds
            do! StrengthReplicaRuntimeLogic.requireOwnerIdle liveRegistry binding.OwnerSessionId

            do!
                liveRegistry.Register binding
                |> Result.mapError (sprintf "StrengthReplica live registration failed: %A")

            return!
                StrengthReplicaRuntimeLogic.claimLiveDecisionCell
                    gate
                    byReplica
                    key
                    registerReplica
                    liveRegistry
                    releaseModel
                    notePhysicalCleanup
                    binding
                    (Roles.roleLabel binding.CanonicalRole)
                    None
        }

    /// Called after the Replica request profile has been bound by XWire, but
    /// before any ordinary Work transform writer. The runtime admission either
    /// allows this outbound request (mirroring proceeds) or retires it, so the
    /// transform aborts the child before any physical N+1 provider send.
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
        | None -> clearOrphanSession sessionId

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
                        removeState
                        notePhysicalCleanup
                        liveRegistry
                        releaseModel
                        binding
        }

    /// DELEGATE-6.2: the prepared stage. Creates the replica child session
    /// without sending any prompt and reserves internal identity (live
    /// registration plus in-flight completion cell). Model capacity is NOT
    /// acquired here: an empty child never pre-occupies a model slot. The caller
    /// persists DelegationBound against the returned ReplicaSessionId and only
    /// then calls SendPreparedPrompt. On DelegationBound write failure the
    /// caller cleans the empty child through CancelOwner.
    member this.PrepareReplicaStart
        (
            owner: SessionId,
            decisionId: StrengthDecisionId,
            targetProviderRun: ProviderRunIdentity,
            requestedRounds: ReadonlyRoundBudget,
            replicaAgent: string,
            localizedMirror: WireMessage list,
            mirrorSemanticDigest: string
        ) : Task<Result<StrengthReplicaPreparation, string>> =
        taskResult {
            do! StrengthReplicaRuntimeLogic.requireNonEmptyBudget requestedRounds
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

            // The replica executes the inherited owner role read-only. Its identity
            // and model-routing role remain identical for the whole physical request.
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

            let capabilities =
                PromptAuthority.toolCapabilitiesFor ownerProfile.CanonicalRole ProviderRequestKind.StrengthReplica

            let binding: StrengthReplicaBinding =
                { OwnerSessionId = owner
                  ReplicaSessionId = replica
                  DecisionId = decisionId
                  TargetProviderRun = targetProviderRun
                  CanonicalRole = ownerProfile.CanonicalRole
                  RequestedRounds = requestedRounds
                  SemanticDigest = mirrorSemanticDigest
                  LocalizedMirrorMessages = localizedMirror
                  ToolCapabilitySet = capabilities }

            do!
                StrengthReplicaRuntimeLogic.registerLiveOrAbort
                    sessions
                    liveRegistry
                    releaseModel
                    notePhysicalCleanup
                    binding
                    replica

            let state =
                StrengthReplicaRuntimeLogic.createLiveDecisionState binding replicaAgent (Some identitySeed)

            do!
                StrengthReplicaRuntimeLogic.claimCollectorOrFail
                    gate
                    byReplica
                    key
                    liveRegistry
                    sessions
                    releaseModel
                    notePhysicalCleanup
                    registerReplica
                    owner
                    replica
                    replicaAgent
                    state

            let _terminalSub =
                sessions.SubscribeTerminal(
                    replica,
                    fun _ outcome ->
                        let turnOutcome =
                            match outcome with
                            | TerminalOutcome.Completed _ -> ReconcileProgram.TurnCompleted
                            | TerminalOutcome.Failed stop -> ReconcileProgram.TurnFailed stop.Reason
                            | TerminalOutcome.Aborted stop -> ReconcileProgram.TurnAborted stop.Reason

                        observeReplicaTurn state turnOutcome
                )

            return
                { ReplicaSessionId = replica
                  Completion = state.Completion.Task }
        }

    /// DELEGATE-6.2: the start stage of a prepared replica. Acquires the model
    /// lease, admits the bootstrap outbound request and only then sends the
    /// prompt. Called after the caller persisted DelegationBound. Idempotent:
    /// an already-admitted bootstrap is not re-sent, so a retry cannot turn into
    /// a free extra provider request.
    member this.SendPreparedPrompt(replicaSessionId: SessionId) : Task<Result<unit, string>> =
        // bootstrapDetachedSend occurs in this stage after state and model are admitted.
        match tryState replicaSessionId with
        | None -> Task.FromResult(Error "StrengthReplica prepared session is not live")
        | Some state when state.SemanticTerminal |> Option.isSome ->
            Task.FromResult(Error "StrengthReplica decision already closed")
        | Some state -> sendPreparedPromptForState state replicaSessionId

    /// Single-step decision entry: prepare the empty replica child, then send
    /// the prompt. Wiring that must persist DelegationBound between the two
    /// stages uses PrepareReplicaStart + SendPreparedPrompt directly instead.
    member this.StartDecision
        (
            owner: SessionId,
            decisionId: StrengthDecisionId,
            targetProviderRun: ProviderRunIdentity,
            requestedRounds: ReadonlyRoundBudget,
            replicaAgent: string,
            localizedMirror: WireMessage list,
            mirrorSemanticDigest: string
        ) : Task<Result<StrengthReplicaOutcome, string>> =
        task {
            match!
                this.PrepareReplicaStart(
                    owner,
                    decisionId,
                    targetProviderRun,
                    requestedRounds,
                    replicaAgent,
                    localizedMirror,
                    mirrorSemanticDigest
                )
            with
            | Error error -> return Error error
            | Ok prepared ->
                match! this.SendPreparedPrompt prepared.ReplicaSessionId with
                | Error error -> return Error error
                | Ok() ->
                    // The completion cell is only ever resolved through the
                    // semantic-terminal path, which always publishes a result.
                    let! result = prepared.Completion
                    return Ok result
        }

    member _.Dispose() =
        let states = lock gate (fun () -> byReplica.Values |> Seq.toList)

        for state in states do
            // First-wins: an already-published semantic terminal is kept, and
            // removeState performs the one physical cleanup per decision.
            complete StrengthReplicaTerminal.Cancelled state
            removeState state

        liveRegistry.Clear()

    /// DELEGATE-011: every replica whose physical tail was really cleaned up
    /// (lease released, live binding retired, child terminated). An id appears
    /// once and the order is by identity, so repeated reads and repeated
    /// physical-tail observations all yield the same accumulation.
    member _.Released() : string array =
        lock gate (fun () -> releasedIds |> Set.toArray)

    /// The subset of `Released` whose end was abnormal: a live decision cut
    /// short by deletion, owner cancellation or dispose, a provider failure, an
    /// invalid frame, or a binding that never became a decision at all.
    member _.Aborted() : string array =
        lock gate (fun () -> abortedIds |> Set.toArray)

    interface IDisposable with
        member this.Dispose() = this.Dispose()
