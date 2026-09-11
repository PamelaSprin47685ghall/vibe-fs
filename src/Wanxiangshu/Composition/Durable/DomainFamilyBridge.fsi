namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Context.Companion
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Provider.Attempt.Fallback

/// Assembly for the domain-owned fact families whose fold now decides on its own
/// slices and returns a change list: the bridge supplies the narrow reads the
/// decision needs, writes each change back into the aggregate, and renders the
/// family's closed rejection into the spine's `FoldRejection`.
module PromptAuthorityProjectionBridge =
    val fold: projection: AgentProjectionSet -> fact: PromptFactCases -> Result<AgentProjectionSet, FoldRejection>

module ProviderFailureProjectionBridge =
    val fold:
        projection: AgentProjectionSet -> fact: ProviderFailureFactCases -> Result<AgentProjectionSet, FoldRejection>

module CompanionProjectionBridge =
    val fold: projection: AgentProjectionSet -> fact: CompanionFactCases -> Result<AgentProjectionSet, FoldRejection>
