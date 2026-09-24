namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Change
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Cognition
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation

/// Pure envelope dispatch. Each bounded projection owns its own fold algorithm;
/// this module only routes facts and decides which refusals are fatal.
module Fold =

    let empty: ProjectionSet =
        { AgentProjections = AgentProjection.empty
          RuntimeId = None }

    let private reject = FoldRejection.reject

    let private settlePromptAuthority events authorityOpt =
        let completesRoad =
            events
            |> List.exists (function
                | RelayEvent.RetirementCommitted retirement ->
                    match retirement.Outcome with
                    | RetirementOutcome.Accepted _ -> true
                    | RetirementOutcome.Continue -> false
                | _ -> false)

        if completesRoad then
            authorityOpt
            |> Option.map Wanxiangshu.Interaction.Authority.PromptAuthorityLedger.closeCompletedHumanRootManager
        else
            authorityOpt

    let private foldRelay (projection: AgentProjectionSet) (fact: RelayFactCases) =
        match fact with
        | RelayFactCases.TransactionCommitted payload ->
            let sessionId = SessionId.create (RoadId.value payload.RoadId)
            let events = RelayTransaction.events payload.Transaction

            AgentProjection.tryUpdate
                sessionId
                (fun session ->
                    let current =
                        session.Relay |> Option.defaultValue Wanxiangshu.Mission.Relay.Fold.empty

                    let updatedPromptAuthority = session.PromptAuthority |> settlePromptAuthority events

                    Wanxiangshu.Mission.Relay.Fold.apply current payload.RoadId payload.Transaction
                    |> Result.map (fun updated ->
                        { session with
                            Relay = Some updated
                            PromptAuthority = updatedPromptAuthority }))
                projection
            |> Result.mapError (fun reason -> { Fact = "Relay"; Reason = reason })

    /// One cognitive commit folded into the aggregate index.
    ///
    /// Its own function so the family's decision is named rather than nested inside
    /// the dispatcher: the dispatcher routes, this folds.
    let private foldCognition (projection: AgentProjectionSet) (cognition: AssumeFactCases.T) =
        let ownerKey = CognitiveOwner.keyOfFact cognition
        let current = Map.tryFind ownerKey projection.Cognition

        /// One committed workspace bound into the aggregate index.
        let bindWorkspace (acc: AgentProjectionSet) (change: CognitiveProjectionChange) =
            match change with
            | CognitiveProjectionChange.CognitiveSet(key, workspace) ->
                { acc with
                    Cognition = Map.add key workspace acc.Cognition }

        match CognitiveFactFold.fold current cognition with
        | Ok changes -> Ok(List.fold bindWorkspace projection changes)
        | Error rejection ->
            Error
                { Fact = CognitiveFoldRejection.fact rejection
                  Reason = CognitiveFoldRejection.message rejection }

    let foldAgentFact (projection: AgentProjectionSet) (fact: AgentFact) : Result<AgentProjectionSet, FoldRejection> =
        // DSL-003: one dispatch per bounded-context family; each family folds
        // through its own branch so no fold depends on the whole catalogue.
        match fact with
        | AgentFact.Prompt prompt -> PromptAuthorityProjectionBridge.fold projection prompt
        | AgentFact.ProviderFailure failure -> ProviderFailureProjectionBridge.fold projection failure
        | AgentFact.Relay relay -> foldRelay projection relay
        | AgentFact.Execution execution -> DelegationProjectionBridge.foldExecution projection execution
        | AgentFact.ChatExecution chatExecution ->
            ChatExecutionFactFold.fold projection.ChatExecutions chatExecution
            |> Result.map (fun updated ->
                { projection with
                    ChatExecutions = updated })
        | AgentFact.Cognition cognition -> foldCognition projection cognition
        | AgentFact.Orchestrator orchestrator ->
            // The Change family fold consumes the journal-owned `ProjectionSet`,
            // so its assembly stays with the dispatcher, downstream of that edge.
            OrchestratorFactFold.fold projection.Orchestrator orchestrator
            |> Result.map (fun updated ->
                { projection with
                    Orchestrator = updated })
            |> Result.mapError (fun rejection ->
                { Fact = OrchestratorFoldRejection.fact rejection
                  Reason = OrchestratorFoldRejection.message rejection })
        | AgentFact.Companion companion -> CompanionProjectionBridge.fold projection companion
        | AgentFact.Context context -> ContextProjectionBridge.fold projection context
        | AgentFact.Host host -> HostFactFold.fold projection host
        | AgentFact.Fission fission -> ProjectionUpdate.applyFission projection fission
        | AgentFact.Delegation delegation -> DelegationProjectionBridge.foldDelegation projection delegation
        | AgentFact.Attention attention -> ProjectionUpdate.applyAttention projection attention
        | AgentFact.Concern concern -> ProjectionUpdate.applyConcern projection concern
        | AgentFact.InstitutionalLearning learning ->
            ProjectionUpdate.applyInstitutionalLearning projection learning
            |> Result.bind (fun updated -> ProjectionUpdate.applyAttentionLearning updated learning)

    /// Fact-only fold for callers that do not need envelope metadata.
    /// RuntimeStarted needs no envelope field (RuntimeId is in the payload).
    let foldFact (projection: ProjectionSet) (fact: Fact) : Result<ProjectionSet, FoldRejection> =
        match fact with
        | Runtime(RuntimeStarted runtime) ->
            Ok
                { projection with
                    RuntimeId = Some runtime.RuntimeId
                    AgentProjections =
                        { projection.AgentProjections with
                            RuntimeStartCount = projection.AgentProjections.RuntimeStartCount + 1 } }
        | Agent fact ->
            foldAgentFact projection.AgentProjections fact
            |> Result.map (fun agents ->
                { projection with
                    AgentProjections = agents })

    let foldEnvelope (projection: ProjectionSet) (envelope: Envelope) : Result<ProjectionSet, FoldRejection> =
        match envelope.Fact with
        | Runtime(RuntimeStarted runtime) ->
            // Historical workspace runtime watermark. Prompt claims retain the
            // watermark at registration for audit/backward-compatible replay, but
            // restart count no longer drives recovery or automatic abandonment.
            Ok
                { projection with
                    RuntimeId = Some runtime.RuntimeId
                    AgentProjections =
                        { projection.AgentProjections with
                            RuntimeStartCount = projection.AgentProjections.RuntimeStartCount + 1 } }

        | Agent fact ->
            foldAgentFact projection.AgentProjections fact
            |> Result.map (fun agents ->
                { projection with
                    AgentProjections = agents })
// Historical enumeration intentionally has no Journal-owned API. Boot and
// live facts both enter through CanonicalIntegrator, which invokes only
// foldEnvelope for one already-ordered durable event at a time.
