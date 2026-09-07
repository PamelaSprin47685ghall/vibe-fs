namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Durable

module ProviderFailureFactFold =
    val fold:
        projection: AgentProjectionSet -> fact: ProviderFailureFactCases -> Result<AgentProjectionSet, FoldRejection>
