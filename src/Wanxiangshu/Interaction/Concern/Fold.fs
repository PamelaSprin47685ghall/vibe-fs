namespace Wanxiangshu.Interaction.Concern

open Wanxiangshu.Composition.Durable

[<RequireQualifiedAccess>]
module ConcernFactFold =

    let fold (projection: AgentProjectionSet) fact =
        ConcernProjection.applyFact fact projection.Concern
        |> Result.map (fun updated -> { projection with Concern = updated })
        |> Result.mapError (fun reason ->
            { Fact = "Concern"
              Reason = reason })

