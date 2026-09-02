namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact

module ExecutionFactFold =
    val fold: projection: AgentProjectionSet -> fact: ExecutionFactCases -> Result<AgentProjectionSet, FoldRejection>
