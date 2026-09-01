namespace Wanxiangshu.Mission.Review

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity

module ReviewFactFold =

    val fold: projection: AgentProjectionSet -> fact: ReviewFactCases -> Result<AgentProjectionSet, FoldRejection>
