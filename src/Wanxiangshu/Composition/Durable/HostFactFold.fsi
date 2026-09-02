namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Host

module HostFactFold =
    val fold: projection: AgentProjectionSet -> fact: HostFactCases -> Result<AgentProjectionSet, FoldRejection>
