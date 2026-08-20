namespace Wanxiangshu.Enforcer.InstitutionalLearning

open Wanxiangshu.Composition.Durable

[<RequireQualifiedAccess>]
module InstitutionalLearningFactFold =

    let fold projection fact =
        Ok
            { projection with
                InstitutionalLearning = InstitutionalLearningProjection.apply fact projection.InstitutionalLearning }

