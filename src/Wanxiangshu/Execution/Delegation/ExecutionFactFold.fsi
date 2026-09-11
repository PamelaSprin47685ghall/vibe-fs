namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Composition.Durable

module ExecutionFactFold =
    val fold: projection: AgentProjectionSet -> fact: ExecutionFactCases -> Result<AgentProjectionSet, FoldRejection>
