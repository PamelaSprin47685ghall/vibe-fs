namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Composition.Durable

module DelegationFactFold =
    val fold: projection: AgentProjectionSet -> fact: DelegationFactCases -> Result<AgentProjectionSet, FoldRejection>
