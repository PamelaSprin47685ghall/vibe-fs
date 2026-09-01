namespace Wanxiangshu.Execution.Fission

open Wanxiangshu.Composition.Durable

module FissionFactFold =
    val fold: projection: AgentProjectionSet -> fact: FissionFactCases -> Result<AgentProjectionSet, FoldRejection>
