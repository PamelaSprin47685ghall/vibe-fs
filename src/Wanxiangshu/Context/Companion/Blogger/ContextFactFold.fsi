namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion

[<RequireQualifiedAccess>]
module ContextFactFold =
    val fold: projection: AgentProjectionSet -> fact: ContextFactCases -> Result<AgentProjectionSet, FoldRejection>
