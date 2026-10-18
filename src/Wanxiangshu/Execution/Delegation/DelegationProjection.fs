namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

type DelegationSessionState =
    { Handles: AgentLinkageProjection option
      ToolEstimate: DelegatedToolEstimateProjectionState option }

type DelegationProjectionChange =
    | ReplaceSessionState of sessionId: SessionId * state: DelegationSessionState
    | IndexChildHandle of childSessionId: SessionId * record: HandleRecord
    | TerminatedChildHandle of parentSessionId: SessionId * childSessionId: SessionId
    | TerminatedChildWork of work: HandleWorkId * logicalRunId: LogicalRunId

type DelegationFoldRejection =
    | HandleBindingConflict of fact: string
    | HandleNeverLinked of fact: string
    | HandleCompletionMissing of fact: string
    | WorkRejected of fact: string * reason: HandleTransitionRejection
    | ToolEstimateNegative of expectedToolCalls: int

module DelegationSessionState =
    let empty: DelegationSessionState = { Handles = None; ToolEstimate = None }

module DelegationFoldRejection =
    let fact rejection =
        match rejection with
        | HandleBindingConflict factName -> factName
        | HandleNeverLinked factName -> factName
        | HandleCompletionMissing factName -> factName
        | WorkRejected(factName, _) -> factName
        | ToolEstimateNegative _ -> "DelegatedToolEstimateReplaced"

    let message rejection =
        match rejection with
        | HandleBindingConflict _ -> "one handle cannot change its durable binding"
        | HandleNeverLinked _ -> "handle completion or retirement for a handle that was never linked"
        | HandleCompletionMissing _ -> "join retired a handle that had no completion (EXEC-004)"
        | WorkRejected(_, reason) -> sprintf "exact child work rejected: %A" reason
        | ToolEstimateNegative _ -> "expected tool calls must be a non-negative integer"
