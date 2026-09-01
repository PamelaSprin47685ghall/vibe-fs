namespace Wanxiangshu.Interaction.Attention

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Enforcer.InstitutionalLearning

[<RequireQualifiedAccess>]
module AttentionFactFold =
    val fold: projection: AgentProjectionSet -> fact: AttentionFactCases -> Result<AgentProjectionSet, 'error>

    val foldLearning:
        projection: AgentProjectionSet -> fact: InstitutionalLearningFactCases -> Result<AgentProjectionSet, 'error>
