namespace Wanxiangshu.Recovery

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Domain
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Kernel.Identity

/// Durable fallback evidence. Read-only; FallbackLedger is the only writer.
module FallbackEvidence =

    let tryCurrentState (sessionId: SessionId) (projection: ProjectionSet) : FallbackProjection option =
        AgentProjection.tryFind sessionId projection.AgentProjections
        |> Option.bind (fun session -> session.Fallback)

    let currentCursor (sessionId: SessionId) (projection: ProjectionSet) : AgentPairCursor.FallbackCursor option =
        tryCurrentState sessionId projection
        |> Option.map (fun fallback -> fallback.Cursor)

    let currentSide (sessionId: SessionId) (projection: ProjectionSet) : AgentPairCursor.ModelSide option =
        currentCursor sessionId projection
        |> Option.map (fun cursor -> AgentPairCursor.side cursor.Offset)

    let effectiveAgent
        (sessionId: SessionId)
        (projection: ProjectionSet)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : string =
        currentCursor sessionId projection
        |> Option.map (PromptAuthority.effectiveAgentFor profile)
        |> Option.defaultValue profile.SelectedAgent

    let mayContinue (budget: int) (sessionId: SessionId) (projection: ProjectionSet) : bool =
        tryCurrentState sessionId projection
        |> Option.map (FallbackProjection.mayContinue budget)
        |> Option.defaultValue false
