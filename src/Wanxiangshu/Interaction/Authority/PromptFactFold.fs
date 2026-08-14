namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal.ProjectionUpdate

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
