namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Change
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Foundation.Identity

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
