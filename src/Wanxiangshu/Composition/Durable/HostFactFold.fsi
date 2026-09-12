namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Host
open Wanxiangshu.Foundation

module HostFactFold =
    val fold: projection: AgentProjectionSet -> fact: HostFactCases -> Result<AgentProjectionSet, FoldRejection>
