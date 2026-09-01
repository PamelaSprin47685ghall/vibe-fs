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
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Persistence.Journal

/// Pure envelope dispatch. Each bounded projection owns its own fold algorithm;
/// this module only routes facts and decides which refusals are fatal.
module Fold =

    let empty: ProjectionSet =
        { AgentProjections = AgentProjection.empty
          RuntimeId = None }

    let private reject = FoldRejection.reject

    let foldAgentFact (projection: AgentProjectionSet) (fact: AgentFact) : Result<AgentProjectionSet, FoldRejection> =
        // DSL-003: one dispatch per bounded-context family; each family folds
        // through its own branch so no fold depends on the whole catalogue.
        match fact with
        | AgentFact.Prompt prompt -> PromptFactFold.fold projection prompt
        | AgentFact.Fallback fallback -> FallbackFactFold.fold projection fallback
        | AgentFact.Review review -> ReviewFactFold.fold projection review
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

    let private managerLifecycleSessionId fact =
        match fact with
        | ManagerLifecycleFact.LifeOpened payload -> payload.SessionId
        | ManagerLifecycleFact.WorkActivated payload -> payload.SessionId
        | ManagerLifecycleFact.FinalityRequested payload -> payload.SessionId
        | ManagerLifecycleFact.FinalityReviewerEnlisted payload -> payload.SessionId
        | ManagerLifecycleFact.FinalityRejected payload -> payload.SessionId
        | ManagerLifecycleFact.FinalitySiblingSteered payload -> payload.SessionId
        | ManagerLifecycleFact.FinalityBlessed payload -> payload.SessionId
        | ManagerLifecycleFact.FinalityUndecided payload -> payload.SessionId
        | ManagerLifecycleFact.LifeCompleted payload -> payload.SessionId

    let private updateSessionAuthority session fact =
        match fact with
        | ManagerLifecycleFact.LifeCompleted _ ->
            session.PromptAuthority
            |> Option.defaultValue PromptAuthorityLedger.empty
            |> PromptAuthorityLedger.closeCompletedHumanRootManager
            |> Some
        | _ -> session.PromptAuthority

    let private foldManagerLifecycle (projection: ProjectionSet) fact =
        let sessionId = managerLifecycleSessionId fact

        AgentProjection.tryUpdate
            sessionId
            (fun session ->
                let current =
                    session.ManagerLife |> Option.defaultValue ManagerLifecycleProjection.empty

                ManagerLifecycleProjection.fold current fact
                |> Result.map (fun updated ->
                    let authority = updateSessionAuthority session fact

                    { session with
                        ManagerLife = Some updated
                        PromptAuthority = authority }))
            projection.AgentProjections
        |> Result.map (fun agents ->
            { projection with
                AgentProjections = agents })
        |> Result.mapError (fun _ ->
            { Fact = "ManagerLifecycle"
              Reason = "Manager lifecycle fact violates GLORY-012/037 (Life or request identity mismatch)" })

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
        | ManagerLifecycle fact -> foldManagerLifecycle projection fact
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
        | ManagerLifecycle fact ->
            // GLORY-010: lifecycle facts fold onto the session's lifecycle
            // projection. Replays are idempotent inside the projection fold;
            // every rejection names a line no correct writer produces (fatal).
            foldManagerLifecycle projection fact

// Historical enumeration intentionally has no Journal-owned API. Boot and
// live facts both enter through CanonicalIntegrator, which invokes only
// foldEnvelope for one already-ordered durable event at a time.
