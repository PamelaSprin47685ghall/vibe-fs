namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type DelegationHandoffWindow = { Range: XTraceRange; IsInitial: bool }

type DelegationWorkRecordCapability =
    { ParentWorkRecord: SessionId -> Task<string option>
      ParentWorkRecordBounded: SessionId -> XTraceRange -> Task<string option> }

type PreparedDelegationHandoff =
    { Route: DelegationHandoffRoute
      ParentStartInclusive: XTraceCursor
      ParentRecord: string option
      ParentEndExclusive: XTraceCursor }

type HandoffCheckpointIdentity =
    { Parent: SessionId
      Route: DelegationHandoffRoute }

[<RequireQualifiedAccess>]
type HandoffCheckpointCommitment =
    | Committed
    | NotCommitted of reason: string
    | Unknown of reason: string
    | PhaseConflict of reason: string

type HandoffCheckpointSettlement =
    { Identity: HandoffCheckpointIdentity
      Commitment: HandoffCheckpointCommitment }

[<RequireQualifiedAccess>]
module HandoffCheckpointSettlement =
    val committed: parent: SessionId -> handoff: PreparedDelegationHandoff -> HandoffCheckpointSettlement
    val notCommitted:
        parent: SessionId -> handoff: PreparedDelegationHandoff -> reason: string -> HandoffCheckpointSettlement
    val unknown:
        parent: SessionId -> handoff: PreparedDelegationHandoff -> reason: string -> HandoffCheckpointSettlement
    val phaseConflict:
        parent: SessionId -> handoff: PreparedDelegationHandoff -> reason: string -> HandoffCheckpointSettlement

type ReusableHandoffPort =
    { Prepare: SessionId -> DelegationHandoffRoute -> Task<PreparedDelegationHandoff>
      CheckpointCompleted: SessionId -> PreparedDelegationHandoff -> Task<HandoffCheckpointSettlement> }

[<RequireQualifiedAccess>]
module DelegationHandoff =
    val key: parent: SessionId -> route: DelegationHandoffRoute -> string
    val window: previousEnd: XTraceCursor option -> currentEnd: XTraceCursor -> DelegationHandoffWindow
    val childRange: startInclusive: XTraceCursor -> endExclusive: XTraceCursor -> XTraceRange
    val promptDocument: charge: string -> parentRecord: string option -> LlmFacing.Document
    val renderPrompt: charge: string -> parentRecord: string option -> string
    val appendParentDelta: providerPrompt: LlmFacing.Document -> parentRecord: string option -> LlmFacing.Document
