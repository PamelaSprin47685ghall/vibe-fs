namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

module internal SyncDelegatePhysicalIdentity =
    val title: scope: ReuseScopeId -> role: SyncDelegateRole -> agentName: string -> string

type SyncDelegateRuntime =
    new:
        sessions: ISessionHostPort *
        dispatcher: PromptDispatcher.Runtime *
        journal: AgentJournal *
        attached: IAttachedSessionPort *
        onDelegateReady: (SessionId -> string -> unit) *
        quiescence: ISessionQuiescenceGate *
        workRecordFor: (SessionId -> XTraceRange -> ProviderRunIdentity -> Task<string option>) *
        handoff: ReusableHandoffPort *
        ?workspaceDirectory: string *
        ?onInspectorPrompt: (string -> string -> unit) *
        ?onInspectorAnswer: (string -> string -> unit) *
        ?onInspectorCleanup: (string -> unit) ->
            SyncDelegateRuntime

    member Attached: IAttachedSessionPort

    member ObserveProviderToolCall:
        ownerSessionId: SessionId * providerRun: ProviderRunIdentity * role: SyncDelegateRole * callId: ToolCallId ->
            unit

    member TryObservedBatch:
        ownerSessionId: SessionId * providerRun: ProviderRunIdentity * role: SyncDelegateRole * currentCall: ToolCallId ->
            SyncDelegateBatch option

    member TryFind: ownerSessionId: SessionId * role: SyncDelegateRole -> SessionId option
    member TryFindDelegateOwner: delegateSessionId: SessionId -> SessionId option
    member TryFindForScopeClose: ownerSessionId: SessionId * role: SyncDelegateRole -> SessionId option
    member StageDeletedInspector: ownerSessionId: SessionId * inspectorSessionId: SessionId -> bool
    member StageDeletedInspectorBySession: inspectorSessionId: SessionId -> SessionId option

    member Invoke:
        ownerSessionKey: string * role: SyncDelegateRole * charge: string * ?expectedToolCalls: int ->
            Task<Result<string, string>>

    member InvokePrepared:
        ownerSessionKey: string *
        role: SyncDelegateRole *
        charge: string *
        prepareProviderPrompt: (unit -> Task<LlmFacing.Document>) *
        ?expectedToolCalls: int ->
            Task<Result<string, string>>

    member InvokeBatchPrepared:
        ownerSessionKey: string *
        role: SyncDelegateRole *
        charge: string *
        batch: SyncDelegateBatch *
        prepareProviderPrompt: (unit -> Task<LlmFacing.Document>) *
        ?expectedToolCalls: int ->
            Task<Result<SyncDelegateInvocationResult, string>>

    member HandleTurn: turn: ReconciledTurn * permit: QuiescencePermit option -> Task<bool>
    member HasOpeningCursor: sessionId: SessionId -> bool
    member AwaitAssignmentReady: sessionId: SessionId -> Task<bool>
    member CancelSession: sessionId: SessionId -> unit
    member Dispose: unit -> unit
    interface IDisposable
