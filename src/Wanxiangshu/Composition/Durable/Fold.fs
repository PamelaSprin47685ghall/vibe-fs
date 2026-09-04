namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Change
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Persistence.Journal

/// Pure envelope dispatch. Each bounded projection owns its own fold algorithm;
/// this module only routes facts and decides which refusals are fatal.
module Fold =

    let empty: ProjectionSet =
        { AgentProjections = AgentProjection.empty
          RuntimeId = None }

    let private reject = FoldRejection.reject

    let private foldRelay (projection: AgentProjectionSet) (fact: RelayFactCases) =
        match fact with
        | RelayFactCases.TransactionCommitted payload ->
            let sessionId = SessionId.create (RoadId.value payload.RoadId)

            AgentProjection.tryUpdate
                sessionId
                (fun session ->
                    let current = session.Relay |> Option.defaultValue Wanxiangshu.Mission.Relay.Fold.empty

                    Wanxiangshu.Mission.Relay.Fold.apply current payload.RoadId payload.Transaction
                    |> Result.map (fun updated -> { session with Relay = Some updated }))
                projection
            |> Result.mapError (fun reason ->
                { Fact = "Relay"
                  Reason = reason })

    let foldAgentFact (projection: AgentProjectionSet) (fact: AgentFact) : Result<AgentProjectionSet, FoldRejection> =
        // DSL-003: one dispatch per bounded-context family; each family folds
        // through its own branch so no fold depends on the whole catalogue.
        match fact with
        | AgentFact.Prompt prompt -> PromptFactFold.fold projection prompt
        | AgentFact.Fallback fallback -> FallbackFactFold.fold projection fallback
        | AgentFact.Relay relay -> foldRelay projection relay
        | AgentFact.Execution execution -> ExecutionFactFold.fold projection execution
        | AgentFact.ChatExecution chatExecution ->
            ChatExecutionFactFold.fold projection.ChatExecutions chatExecution
            |> Result.map (fun updated ->
                { projection with
                    ChatExecutions = updated })
        | AgentFact.Orchestrator orchestrator -> OrchestratorFactFold.fold projection orchestrator
        | AgentFact.Companion companion -> CompanionFactFold.fold projection companion
        | AgentFact.Context context -> ContextFactFold.fold projection context
        | AgentFact.Host host -> HostFactFold.fold projection host
        | AgentFact.Fission fission -> FissionFactFold.fold projection fission
        | AgentFact.Delegation delegation -> DelegationFactFold.fold projection delegation
        | AgentFact.Attention attention -> AttentionFactFold.fold projection attention
        | AgentFact.Concern concern -> ConcernFactFold.fold projection concern
        | AgentFact.InstitutionalLearning learning ->
            InstitutionalLearningFactFold.fold projection learning
            |> Result.bind (fun updated -> AttentionFactFold.foldLearning updated learning)

    let private foldMagicTodo (projection: ProjectionSet) (eventId: EventId) (fact: MagicTodoFacts.MagicTodoFact) =
        match fact with
        | MagicTodoFacts.MagicTodoFact.PrefixRebaseCommittedV2 rebase ->
            ProjectionUpdate.tryUpdatePrefix
                rebase.SessionId
                (PrefixEpochProjection.applyRebase
                    rebase.PreviousEpochId
                    rebase.NextEpochId
                    { FrozenRecordPrefixRef = rebase.FrozenRecordPrefixRef
                      FrozenRecordPrefixDigest = rebase.FrozenRecordPrefixDigest
                      CutoffExclusive = rebase.CutoffExclusive
                      CoveredPrefixDigest = rebase.CoveredPrefixDigest
                      SealRoot = rebase.SealRoot
                      SyntheticMessageId = rebase.SyntheticMessageId })
                projection.AgentProjections
            |> ProjectionUpdate.prefixOutcome "PrefixRebaseCommittedV2" projection.AgentProjections
            |> Result.map (fun agents ->
                { projection with
                    AgentProjections = agents })
        | fact ->
            MagicTodoProjection.fold eventId projection.AgentProjections.MagicTodo fact
            |> Result.mapError (fun rejection ->
                { Fact = "MagicTodo"
                  Reason = sprintf "%A" rejection })
            |> Result.map (fun magicTodo ->
                { projection with
                    AgentProjections =
                        { projection.AgentProjections with
                            MagicTodo = magicTodo } })

    /// Fact-only fold for callers that do not need envelope metadata.
    /// RuntimeStarted needs no envelope field (RuntimeId is in the payload).
    /// MagicTodo requires an EventId and must go through foldEnvelope.
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
        | MagicTodo _ -> reject "MagicTodo" "foldFact does not support MagicTodo; use foldEnvelope with an EventId"

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
        | MagicTodo fact -> foldMagicTodo projection envelope.EventId fact
// Historical enumeration intentionally has no Journal-owned API. Boot and
// live facts both enter through CanonicalIntegrator, which invokes only
// foldEnvelope for one already-ordered durable event at a time.
