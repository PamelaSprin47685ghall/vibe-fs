namespace Wanxiangshu.Interaction.Concern

open Wanxiangshu.Composition.Durable

[<RequireQualifiedAccess>]
module ConcernFactFold =
    val fold: projection: AgentProjectionSet -> fact: ConcernFactCases -> Result<AgentProjectionSet, FoldRejection>
