namespace Wanxiangshu.Interaction.Authority

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
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Composition.Durable

module PromptFactFold =

    let private reject = FoldRejection.reject

    let fold (projection: AgentProjectionSet) (fact: PromptFactCases) : Result<AgentProjectionSet, FoldRejection> =
        match fact with
        // ── prompt dispatch ─────────────────────────────────────────────────

        | PromptFactCases.PluginPromptClaimed payload ->
            Ok(
                updateAuthority
                    payload.SessionId
                    (fun authority ->
                        PromptAuthorityLedger.foldPromptClaimed projection.RuntimeStartCount authority payload)
                    projection
            )

        | PromptFactCases.PluginPromptSubmitted payload ->
            Ok(
                updateAuthority
                    payload.SessionId
                    (fun authority -> PromptAuthorityLedger.foldPromptSubmitted authority payload)
                    projection
            )

        | PromptFactCases.PluginPromptPhysicalAccepted payload ->
            Ok(
                updateAuthority
                    payload.SessionId
                    (fun authority -> PromptAuthorityLedger.foldPromptPhysicalAccepted authority payload)
                    projection
            )

        | PromptFactCases.PluginPromptAbandoned payload ->
            Ok(
                updateAuthority
                    payload.SessionId
                    (fun authority -> PromptAuthorityLedger.foldPromptAbandoned authority payload)
                    projection
            )

        // ── authority ───────────────────────────────────────────────────────

        | PromptFactCases.AuthorityRootAccepted payload ->
            // FALLBACK-001: a new Authority Root starts a fresh cursor. Done here
            // rather than by a separate reset fact, because the reset is not an
            // independent event — it IS this fact.
            //
            // REVIEW-007: a HumanRoot also creates a review requirement. An
            // AgentOwnerRoot does not: the agent that forked the work is
            // accountable for it, and requiring review of every internal prompt
            // would make the Guard fire on its own continuations.
            let withAuthority =
                updateSession
                    payload.SessionId
                    (fun session ->
                        { session with
                            PromptAuthority =
                                Some(
                                    PromptAuthorityLedger.foldAuthorityRootAccepted
                                        (Option.defaultValue PromptAuthorityLedger.empty session.PromptAuthority)
                                        payload
                                )
                            Fallback =
                                Some(
                                    FallbackProjection.forAuthority
                                        payload.LogicalRunId
                                        payload.AuthorityRootUserMessageId
                                ) })
                    projection

            if payload.AuthorityKind = "HumanRoot" then
                Ok(
                    updateRequirements
                        payload.SessionId
                        (ReviewRequirementProjection.addRequirement payload.SessionId payload.AuthorityRootUserMessageId)
                        withAuthority
                )
            else
                Ok withAuthority
