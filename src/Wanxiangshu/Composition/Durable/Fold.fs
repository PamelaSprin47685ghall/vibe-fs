namespace Wanxiangshu.Composition.Durable

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
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Composition.Durable
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
        | AgentFact.Orchestrator orchestrator -> OrchestratorFactFold.fold projection orchestrator
        | AgentFact.Companion companion -> CompanionFactFold.fold projection companion
        | AgentFact.Context context -> ContextFactFold.fold projection context
        | AgentFact.Host host -> HostFactFold.fold projection host
        | AgentFact.Fission fission -> FissionFactFold.fold projection fission
        | AgentFact.Delegation delegation -> DelegationFactFold.fold projection delegation

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
        | MagicTodo payload ->
            match MagicTodoFactCodec.tryDecode payload with
            | Error reason -> reject "MagicTodo" ("invalid canonical payload: " + reason)
            | Ok(MagicTodoFacts.MagicTodoFact.PrefixRebaseCommittedV2 rebase) ->
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
            | Ok fact ->
                MagicTodoProjection.fold envelope.EventId projection.AgentProjections.MagicTodo fact
                |> Result.mapError (fun rejection ->
                    { Fact = "MagicTodo"
                      Reason = sprintf "%A" rejection })
                |> Result.map (fun magicTodo ->
                    { projection with
                        AgentProjections =
                            { projection.AgentProjections with
                                MagicTodo = magicTodo } })
        | ManagerLifecycle fact ->
            // GLORY-010: lifecycle facts fold onto the session's lifecycle
            // projection. Replays are idempotent inside the projection fold;
            // every rejection names a line no correct writer produces (fatal).
            let sessionId =
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

            AgentProjection.tryUpdate
                sessionId
                (fun session ->
                    let current =
                        session.ManagerLife |> Option.defaultValue ManagerLifecycleProjection.empty

                    ManagerLifecycleProjection.fold current fact
                    |> Result.map (fun updated ->
                        let authority =
                            match fact with
                            | ManagerLifecycleFact.LifeCompleted _ ->
                                session.PromptAuthority
                                |> Option.defaultValue PromptAuthorityLedger.empty
                                |> PromptAuthorityLedger.closeCompletedHumanRootManager
                                |> Some
                            | _ -> session.PromptAuthority

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

// Historical enumeration intentionally has no Journal-owned API. Boot and
// live facts both enter through CanonicalIntegrator, which invokes only
// foldEnvelope for one already-ordered durable event at a time.
