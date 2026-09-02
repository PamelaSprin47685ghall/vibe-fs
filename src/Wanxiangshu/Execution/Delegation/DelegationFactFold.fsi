namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact

module DelegationFactFold =
    val fold: projection: AgentProjectionSet -> fact: DelegationFactCases -> Result<AgentProjectionSet, FoldRejection>
