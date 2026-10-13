namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

module Fold =
    val empty: ProjectionSet

    val foldAgentFact: projection: AgentProjectionSet -> fact: AgentFact -> Result<AgentProjectionSet, FoldRejection>

    val foldFact: projection: ProjectionSet -> fact: Fact -> Result<ProjectionSet, FoldRejection>

    val foldEnvelope: projection: ProjectionSet -> envelope: Envelope -> Result<ProjectionSet, FoldRejection>

    /// A unified S4 Delivered retirement completes the manager life.
    val settleCompletedLife: projection: ProjectionSet -> sessionId: SessionId -> ProjectionSet

    val isOk: result: Result<'T, 'E> -> bool

    val unwrap: result: Result<'T, 'E> -> 'T
