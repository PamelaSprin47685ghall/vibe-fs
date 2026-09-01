namespace Wanxiangshu.Execution.Delegation.Fork

open Wanxiangshu.Execution.Session

module ChildRunProjection =
    val status: runtimeCancelled: bool -> run: ChildRun -> AgentStatus
    val toRecord: runtimeCancelled: bool -> agentId: string -> run: ChildRun -> AgentRecord
