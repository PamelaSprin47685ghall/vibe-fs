namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Persistence.Journal

module Fold =
    val empty: ProjectionSet

    val foldAgentFact: projection: AgentProjectionSet -> fact: AgentFact -> Result<AgentProjectionSet, FoldRejection>

    val foldFact: projection: ProjectionSet -> fact: Fact -> Result<ProjectionSet, FoldRejection>

    val foldEnvelope: projection: ProjectionSet -> envelope: Envelope -> Result<ProjectionSet, FoldRejection>
