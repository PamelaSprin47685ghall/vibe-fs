namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Durable

module FallbackFactFold =
    val fold: projection: AgentProjectionSet -> fact: FallbackFactCases -> Result<AgentProjectionSet, FoldRejection>
