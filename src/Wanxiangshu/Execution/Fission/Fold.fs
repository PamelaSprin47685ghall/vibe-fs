namespace Wanxiangshu.Execution.Fission

open Wanxiangshu.Execution.Session.Recovery

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Composition.Durable

module FissionFactFold =

    let fold (projection: AgentProjectionSet) (fact: FissionFactCases) : Result<AgentProjectionSet, FoldRejection> =
        match FissionProjection.fold projection.Fission fact with
        | Ok fission -> Ok { projection with Fission = fission }
        | Error reason -> FoldRejection.reject "Fission" (sprintf "%A" reason)
