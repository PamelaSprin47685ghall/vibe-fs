namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type internal SyncDelegateTerminalFailureScope =
    | FreshAuthorityRoot of AuthorityRootUserMessageId
    | ExistingAuthorityContinuation of PhysicalUserMessageId

and internal SyncDelegateCall =
    { Owner: SessionId
      OwnerScope: ReuseScopeId
      Role: SyncDelegateRole
      Delegate: SessionId
      Agent: string
      Invocations: SyncDelegateInvocation list
      AcceptedRoot: TaskCompletionSource<AuthorityRootUserMessageId>
      mutable AcceptedAuthorityRoot: AuthorityRootUserMessageId option
      mutable TerminalFailureScope: SyncDelegateTerminalFailureScope option
      Answer: TaskCompletionSource<Result<string, string>> }

and internal SyncDelegateInvocation =
    { Owner: SessionId
      OwnerScope: ReuseScopeId
      Role: SyncDelegateRole
      Charge: string
      ExpectedToolCalls: int option
      PrepareProviderPrompt: unit -> Task<LlmFacing.Document>
      Batch: SyncDelegateBatch option
      Completion: TaskCompletionSource<Result<SyncDelegateInvocationResult, string>>
      mutable StartCursor: int64 option }

[<RequireQualifiedAccess>]
type internal SyncDelegateAdmission =
    | Waiting
    | Ready of SyncDelegateInvocation list
    | Rejected of string

type internal SyncDelegateCallStore =
    new: unit -> SyncDelegateCallStore

    member ObserveProviderToolCall:
        owner: SessionId * providerRun: ProviderRunIdentity * role: SyncDelegateRole * callId: ToolCallId -> unit

    member TryObservedBatch:
        owner: SessionId * providerRun: ProviderRunIdentity * role: SyncDelegateRole * currentCall: ToolCallId ->
            SyncDelegateBatch option

    member TryPeekCallByDelegate: delegateSession: SessionId -> SyncDelegateCall option
    member TryPopCallByDelegate: delegateSession: SessionId -> SyncDelegateCall option
    member FailCall: call: SyncDelegateCall * error: string -> unit
    member Admit: invocation: SyncDelegateInvocation -> SyncDelegateAdmission
    member ReleaseAdmission: ownerScope: ReuseScopeId * role: SyncDelegateRole -> unit
    member CancelScope: scope: ReuseScopeId -> unit

    member BeginCall:
        owner: SessionId *
        ownerScope: ReuseScopeId *
        role: SyncDelegateRole *
        delegateSession: SessionId *
        agent: string *
        invocations: SyncDelegateInvocation list ->
            Result<SyncDelegateCall * IDisposable, string>

    member TryTakeDeletedInspector: scope: ReuseScopeId -> SessionId option
    member TryGetDeletedInspector: scope: ReuseScopeId -> SessionId option
    member PutDeletedInspector: scope: ReuseScopeId * inspectorSessionId: SessionId -> SessionId option
    member ClearDeletedInspector: scope: ReuseScopeId -> SessionId option
    member ClearAll: unit -> SessionId list
