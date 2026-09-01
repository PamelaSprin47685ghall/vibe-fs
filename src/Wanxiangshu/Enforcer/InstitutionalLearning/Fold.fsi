namespace Wanxiangshu.Enforcer.InstitutionalLearning

open Wanxiangshu.Composition.Durable

[<RequireQualifiedAccess>]
module InstitutionalLearningFactFold =
    val fold:
        projection: AgentProjectionSet -> fact: InstitutionalLearningFactCases -> Result<AgentProjectionSet, 'error>
