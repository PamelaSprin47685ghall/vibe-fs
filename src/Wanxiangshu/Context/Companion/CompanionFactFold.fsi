namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Composition.Durable

module CompanionFactFold =
    val fold: projection: AgentProjectionSet -> fact: CompanionFactCases -> Result<AgentProjectionSet, FoldRejection>
