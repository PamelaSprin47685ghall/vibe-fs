namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module DelegatedToolEstimateLedger =
    val tryState: journal: AgentJournal -> sessionId: SessionId -> DelegatedToolEstimateProjectionState option
    val tryRemaining: journal: AgentJournal -> sessionId: SessionId -> int option
    val replace: journal: AgentJournal -> sessionId: SessionId -> expectedToolCalls: int -> Task<unit>
    val observe: journal: AgentJournal -> sessionId: SessionId -> toolCallId: ToolCallId -> Task<unit>
