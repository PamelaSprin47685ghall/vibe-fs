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

type StrengthDryRunStart =
    { ReplicaSessionId: SessionId
      Completion: Task<StrengthReplicaOutcome> }

type StrengthReplicaRuntime =
    new:
        sessions: ISessionHostPort *
        dispatcher: PromptDispatcher.Runtime *
        liveRegistry: StrengthRuntime *
        registerReplica: (SessionId -> SessionId -> string -> unit) *
        ?workspaceDirectory: string *
        ?maxFrameBytes: int *
        ?tryAcquireModel: (SessionId -> string -> OpencodeModel option) *
        ?releaseModel: (SessionId -> unit) ->
            StrengthReplicaRuntime

    member MaxFrameBytes: int
    member IsReplica: sessionId: SessionId -> bool
    member TryOwner: sessionId: SessionId -> SessionId option
    member TryDecision: sessionId: SessionId -> StrengthDecisionId option
    member HandleTransform: output: obj -> Task<bool>
    member HandleTurn: turn: ReconciledTurn -> bool
    member HandleSessionDeleted: sessionId: SessionId -> unit
    member CancelOwner: owner: SessionId -> Task
    member CloseDryRunAtTargetTerminal: turn: ReconciledTurn -> Task

    member StartDryRun:
        owner: SessionId *
        decisionId: StrengthDecisionId *
        targetProviderRun: ProviderRunIdentity *
        budget: StrengthBudget *
        replicaAgent: string *
        localizedMirror: WireMessage list *
        mirrorSemanticDigest: string ->
            Task<Result<StrengthDryRunStart, string>>

    member StartDecision:
        owner: SessionId *
        decisionId: StrengthDecisionId *
        targetProviderRun: ProviderRunIdentity *
        budget: StrengthBudget *
        replicaAgent: string *
        localizedMirror: WireMessage list *
        mirrorSemanticDigest: string ->
            Task<Result<StrengthReplicaOutcome, string>>

    member Dispose: unit -> unit
    interface IDisposable
