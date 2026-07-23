namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact

type ProjectionSet =
    { AgentProjections: AgentProjectionSet
      RuntimeId: RuntimeId option }

type RuntimeSnapshot =
    { Frontier: Frontier
      Projections: ProjectionSet
      OwnRuntimeId: RuntimeId option
      OwnLocalSeq: int64 }

module Fold =

    let empty: ProjectionSet =
        { AgentProjections = AgentFacts.empty
          RuntimeId = None }

    let foldEnvelope (proj: ProjectionSet) (env: Envelope) : ProjectionSet =
        match env.Fact with
        | Runtime(RuntimeStarted r) ->
            { proj with
                RuntimeId = Some r.RuntimeId }
        | Agent agentFact ->
            let agentProj = AgentFacts.foldAgentFact proj.AgentProjections agentFact

            { proj with
                AgentProjections = agentProj }

    let apply (proj: ProjectionSet) (envelopes: Envelope list) : ProjectionSet = List.fold foldEnvelope proj envelopes
