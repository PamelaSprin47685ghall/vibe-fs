namespace Wanxiangshu.Journal

open Wanxiangshu.Kernel.Fact

module FissionFactFold =

    let fold (projection: AgentProjectionSet) (fact: FissionFactCases) : Result<AgentProjectionSet, FoldRejection> =
        match FissionProjection.fold projection.Fission fact with
        | Ok fission -> Ok { projection with Fission = fission }
        | Error reason ->
            FoldRejection.reject "Fission" (sprintf "%A" reason)
