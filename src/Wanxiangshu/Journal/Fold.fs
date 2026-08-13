namespace Wanxiangshu.Journal

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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

    let foldEnvelope (projection: ProjectionSet) (envelope: Envelope) : Result<ProjectionSet, FoldRejection> =
        match envelope.Fact with
        | Runtime(RuntimeStarted runtime) ->
            // PROMPT-011 `RecoveryAttemptBudget`: a plugin start means every claim
            // still pending at this point has survived one more recovery attempt.
            //
            // Counted here rather than written by the recovery routine. A fact saying
            // "I attempted recovery" would itself be written during recovery, so a
            // crash before that write would lose the attempt and the budget could
            // never expire — which is the unbounded-pending state the clause bounds.
            //
            // Replay is exact: envelopes fold in order, so a claim is only counted by
            // the starts that came after it. The claim records the watermark at
            // registration (`ClaimedAtRuntimeStartCount`); this arm only advances
            // the workspace counter — O(1) in the session map, not O(sessions).
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
                        { session with
                            ManagerLife = Some updated }))
                projection.AgentProjections
            |> Result.map (fun agents ->
                { projection with
                    AgentProjections = agents })
            |> Result.mapError (fun _ ->
                { Fact = "ManagerLifecycle"
                  Reason = "Manager lifecycle fact violates GLORY-012/037 (Life or request identity mismatch)" })

    /// Fold a journal. PERSIST-004: the first impossible line stops the fold and
    /// reports which fact and why, rather than producing a partially replayed
    /// state that no writer could have produced.
    let apply (projection: ProjectionSet) (envelopes: Envelope list) : Result<ProjectionSet, FoldRejection> =
        envelopes
        |> List.fold
            (fun state envelope -> state |> Result.bind (fun current -> foldEnvelope current envelope))
            (Ok projection)
