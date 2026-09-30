namespace Wanxiangshu.Strength.Replica

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Strength

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

/// DELEGATE-5.3: identity of one real outbound Replica provider request.
/// PriorProviderRun is the Host identity of the latest assistant response
/// already visible in the transform view (None before the first response).
type StrengthReplicaRequestKey =
    { ReplicaSessionId: SessionId
      PriorProviderRun: ProviderRunIdentity option }

/// DELEGATE-6.2: prepared-stage handle. The empty replica child exists with
/// internal identity, but no prompt was sent and no model capacity reserved.
type StrengthReplicaPreparation =
    { ReplicaSessionId: SessionId
      Completion: Task<StrengthReplicaOutcome> }

type StrengthReplicaPeek =
    { RequestsAdmitted: int
      Batches: StrengthRequestBatch list
      SemanticTerminal: StrengthReplicaTerminal option }

type StrengthReplicaRuntime =
    new:
        sessions: ISessionHostPort *
        dispatcher: PromptDispatcher.Runtime *
        liveRegistry: StrengthRuntime *
        registerReplica: (SessionId -> SessionId -> string -> unit) *
        ?workspaceDirectory: string *
        ?tryAcquireModel: (SessionId -> string -> OpencodeModel option) *
        ?releaseModel: (SessionId -> unit) ->
            StrengthReplicaRuntime

    member IsReplica: sessionId: SessionId -> bool
    member TryOwner: sessionId: SessionId -> SessionId option
    member TryDecision: sessionId: SessionId -> StrengthDecisionId option
    member TryPeek: replicaSessionId: SessionId -> StrengthReplicaPeek option

    /// DELEGATE-6.2 prepared stage: create the empty child and internal
    /// identity without sending a prompt or reserving model capacity. The
    /// caller persists DelegationBound before SendPreparedPrompt.
    member PrepareReplicaStart:
        owner: SessionId *
        decisionId: StrengthDecisionId *
        targetProviderRun: ProviderRunIdentity *
        requestedRounds: ReadonlyRoundBudget *
        replicaAgent: string *
        localizedMirror: WireMessage list *
        mirrorSemanticDigest: string ->
            Task<Result<StrengthReplicaPreparation, string>>

    /// DELEGATE-6.2 start stage: claim the bootstrap once, acquire the model
    /// lease and send. Provider request budget is consumed by the outbound transform.
    member SendPreparedPrompt: replicaSessionId: SessionId -> Task<Result<unit, string>>

    member AttachLiveDecision: binding: StrengthReplicaBinding -> Result<Task<StrengthReplicaOutcome>, string>

    member HandleTransform: output: obj -> Task<bool>
    member HandleTurn: turn: ReconciledTurn -> bool
    member HandleSessionDeleted: sessionId: SessionId -> unit
    member CancelOwner: owner: SessionId -> Task

    /// Single-step decision entry: prepare then send. Wiring that must persist
    /// DelegationBound between the stages uses PrepareReplicaStart followed by
    /// SendPreparedPrompt instead.
    member StartDecision:
        owner: SessionId *
        decisionId: StrengthDecisionId *
        targetProviderRun: ProviderRunIdentity *
        requestedRounds: ReadonlyRoundBudget *
        replicaAgent: string *
        localizedMirror: WireMessage list *
        mirrorSemanticDigest: string ->
            Task<Result<StrengthReplicaOutcome, string>>

    member Dispose: unit -> unit

    member Released: unit -> string array

    member Aborted: unit -> string array

    interface IDisposable
