namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact

module PromptFactFold =
    val fold: projection: AgentProjectionSet -> fact: PromptFactCases -> Result<AgentProjectionSet, FoldRejection>
