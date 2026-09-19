namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

/// delegation-029: Delegation-owned per-session projection state.
/// Durable composition is the sole location that combines this into AgentProjectionSet.
type DelegationSessionState =
    { Handles: AgentLinkageProjection option
      ToolEstimate: DelegatedToolEstimateProjectionState option }

/// delegation-029 / durable-events-023: Pure fold changes emitted by delegation-owned folds.
/// Durable composition is the sole location that interprets and applies these changes.
type DelegationProjectionChange =
    | ReplaceSessionState of sessionId: SessionId * state: DelegationSessionState
    | IndexChildHandle of childSessionId: SessionId * record: HandleRecord
    | MoveHandoffFrontier of key: string * parentEndExclusive: int64
    | TerminatedChildHandle of childSessionId: SessionId

/// delegation-029 / durable-events-023: Closed rejections emitted by delegation-owned folds.
/// Durable composition translates these into durable FoldRejection values.
type DelegationFoldRejection =
    | HandleBindingConflict of fact: string
    | HandleNeverLinked of fact: string
    | HandleCompletionMissing of fact: string
    | HandoffFrontierCannotRetreat of previous: int64 * requested: int64
    | HandoffFrontierNegative of requested: int64
    | ToolEstimateNegative of expectedToolCalls: int

/// delegation-029: Module providing default empty delegation session state.
module DelegationSessionState =
    val empty: DelegationSessionState

/// delegation-029 / durable-events-023: Module providing fact and message renderers for delegation fold rejections.
module DelegationFoldRejection =
    val fact: rejection: DelegationFoldRejection -> string
    val message: rejection: DelegationFoldRejection -> string
