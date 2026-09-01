namespace Wanxiangshu.Change

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact

module OrchestratorFactFold =
    val fold: projection: AgentProjectionSet -> fact: OrchestratorFactCases -> Result<AgentProjectionSet, FoldRejection>
