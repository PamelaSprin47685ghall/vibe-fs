namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

type DelegationSessionState =
    { Handles: AgentLinkageProjection option
      ToolEstimate: DelegatedToolEstimateProjectionState option }

type DelegationProjectionChange =
    | ReplaceSessionState of sessionId: SessionId * state: DelegationSessionState
    | IndexChildHandle of childSessionId: SessionId * record: HandleRecord
    | MoveHandoffFrontier of key: string * parentEndExclusive: int64
    | TerminatedChildHandle of childSessionId: SessionId

type DelegationFoldRejection =
    | HandleBindingConflict of fact: string
    | HandleNeverLinked of fact: string
    | HandleCompletionMissing of fact: string
    | HandoffFrontierCannotRetreat of previous: int64 * requested: int64
    | HandoffFrontierNegative of requested: int64
    | ToolEstimateNegative of expectedToolCalls: int

module DelegationSessionState =
    let empty: DelegationSessionState = { Handles = None; ToolEstimate = None }

module DelegationFoldRejection =
    let fact rejection =
        match rejection with
        | HandleBindingConflict factName -> factName
        | HandleNeverLinked factName -> factName
        | HandleCompletionMissing factName -> factName
        | HandoffFrontierCannotRetreat _ -> "DelegationHandoffCompleted"
        | HandoffFrontierNegative _ -> "DelegationHandoffCompleted"
        | ToolEstimateNegative _ -> "DelegatedToolEstimateReplaced"

    let message rejection =
        match rejection with
        | HandleBindingConflict _ -> "one handle cannot change its durable binding"
        | HandleNeverLinked _ -> "handle completion or retirement for a handle that was never linked"
        | HandleCompletionMissing _ -> "join retired a handle that had no completion (EXEC-004)"
        | HandoffFrontierCannotRetreat _ -> "completed parent handoff frontier cannot retreat"
        | HandoffFrontierNegative _ -> "completed parent handoff frontier must be non-negative"
        | ToolEstimateNegative _ -> "expected tool calls must be a non-negative integer"
