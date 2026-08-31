namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
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
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.OpenCode
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Strength

/// Confirmed-failure recovery. The failed session owns the next slot:
/// WorkMain arms one X prefix opportunity; BloggerMain may insert one BloggerSquash
/// maintenance request before retrying main. No future unrelated X material is a
/// recovery trigger.
module ProviderRecoveryWorkflow =

    let private sessionHasFreshCoverage (projection: ProjectionSet) (sessionId: SessionId) =
        let session = AgentProjection.tryFind sessionId projection.AgentProjections

        let committedCutoff =
            session
            |> Option.bind (fun state -> state.PrefixEpoch)
            |> Option.bind (fun prefix -> prefix.Snapshot)
            |> Option.map (fun snapshot -> snapshot.CutoffExclusive)
            |> Option.defaultValue 0

        session
        |> Option.bind (fun state -> state.Blog)
        |> Option.exists (fun blog -> blog.Coverage.CoverableTurnCutoffExclusive > committedCutoff)

    let private bloggerOfMain (projection: ProjectionSet) (sessionId: SessionId) =
        SessionAssociationProjection.tryBloggerOf sessionId projection.AgentProjections.Associations

    let private hasOpenBloggerRequest
        (projection: ProjectionSet)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        =
        projection.AgentProjections.Sessions
        |> Map.tryFind mainSessionId
        |> Option.bind (fun session -> session.BloggerCycles)
        |> Option.bind (BloggerCycleProjection.tryOpenByBlogger bloggerSessionId)
        |> Option.isSome

    [<RequireQualifiedAccess>]
    type private RecoveryMaterialState =
        | Ready
        | AwaitCommittedFact

    [<RequireQualifiedAccess>]
    type private BloggerContinuationFailure =
        | Materialize of string
        | Send of string
        | Bind of string

    let private recoveryMaterialState
        (projection: ProjectionSet)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        =
        if sessionHasFreshCoverage projection mainSessionId then
            RecoveryMaterialState.Ready
        elif bloggerOfMain projection mainSessionId <> Some bloggerSessionId then
            RecoveryMaterialState.Ready
        elif hasOpenBloggerRequest projection mainSessionId bloggerSessionId then
            RecoveryMaterialState.AwaitCommittedFact
        else
            RecoveryMaterialState.Ready

    /// CTX-023 / PAR-018: durable event wait. The open materialization is the
    /// producer proof; commit/abandon facts close it and coverage facts may make
    /// a probe available. No process-local flight state and no clock participate.
    let rec private awaitLinkedProducer
        (host: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        : Task =
        task {
            let projection, revision = AgentJournal.snapshotWithRevision durable

            return!
                awaitRecoveryMaterialState
                    host
                    durable
                    mainSessionId
                    bloggerSessionId
                    revision
                    (recoveryMaterialState projection mainSessionId bloggerSessionId)
        }

    and private awaitRecoveryMaterialState
        (host: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        revision
        state
        : Task =
        match state with
        | RecoveryMaterialState.Ready -> Task.FromResult(()) :> Task
        | RecoveryMaterialState.AwaitCommittedFact ->
            awaitRecoveryMaterialEvent host durable mainSessionId bloggerSessionId revision

    and private awaitRecoveryMaterialEvent
        (host: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        revision
        : Task =
        task {
            match! AgentJournal.awaitChangeFromOrCancel revision host.Cancellation durable with
            | None -> return ()
            | Some _ -> return! awaitLinkedProducer host durable mainSessionId bloggerSessionId
        }

    let awaitRecoveryMaterial (host: IBloggerRuntimeHost) (durable: AgentJournal) (mainSessionId: SessionId) : Task =
        let projection = AgentJournal.snapshot durable

        match bloggerOfMain projection mainSessionId with
        | None -> Task.FromResult(()) :> Task
        | Some bloggerSessionId -> awaitLinkedProducer host durable mainSessionId bloggerSessionId

    let private mainSessionOfBloggerProjection (projection: ProjectionSet) (bloggerSessionId: SessionId) =
        SessionAssociationProjection.tryMainSessionOf bloggerSessionId projection.AgentProjections.Associations

    let private mainSessionOfBlogger (durable: AgentJournal) (bloggerSessionId: SessionId) =
        mainSessionOfBloggerProjection (AgentJournal.snapshot durable) bloggerSessionId

    let private requestKindFor (durable: AgentJournal) (scope: IBloggerRuntimeHost) (sessionId: SessionId) =
        match mainSessionOfBlogger durable sessionId, scope.TryPeekCurrentRequest(SessionId.value sessionId) with
        | Some _, Some(BloggerRequestContext.Squash _) -> Some ProviderRequestKind.BloggerSquash
        | Some _, Some(BloggerRequestContext.Main _) -> Some ProviderRequestKind.BloggerMain
        | Some _, None -> None
        | None, _ -> Some ProviderRequestKind.WorkMain

    let private recoverySquashContext (durable: AgentJournal) (mainSessionId: SessionId) (bloggerSessionId: SessionId) =
        let session =
            AgentProjection.tryFind mainSessionId (AgentJournal.snapshot durable).AgentProjections

        let blog =
            session
            |> Option.bind (fun value -> value.Blog)
            |> Option.defaultValue BlogProjection.empty

        let epoch =
            session
            |> Option.bind (fun value -> value.PrefixEpoch)
            |> Option.map (fun prefix -> prefix.EpochId)
            |> Option.defaultValue PrefixEpochId.initial

        CompanionHostBlogger.tryBuildSquashContext mainSessionId bloggerSessionId epoch blog

    let private squashPrompt (mainSessionId: SessionId) =
        ProviderProse.instructionLines (ProviderProse.languageOf mainSessionId) CompanionPrompt.Squash Map.empty
        |> CompanionPrompt.asCommentedInstruction

    let private notifyFailure (eventPort: IEventObservationPort) (turn: ReconciledTurn) (reason: string) =
        eventPort.NotifyTerminal
            turn.SessionId
            (TerminalOutcome.Failed(TerminalStop.forAuthority turn.AuthorityRootUserMessageId reason))
        |> ignore

    let private sendContinuation
        (sessionPort: ISessionHostPort)
        (turn: ReconciledTurn)
        (journal: AgentJournal option)
        (prompt: string)
        =
        HostSessionNudge.sendContinuationResult
            sessionPort
            turn.SessionId
            prompt
            PromptAuthority.ContinuationKind.ProviderRetryAttempt
            turn.Directory
            journal
            PromptDispatcher.AwaitMode.Detached
            None

    let private handleContinuation
        (eventPort: IEventObservationPort)
        (turn: ReconciledTurn)
        (error: string)
        (continuation: Result<PromptKey, string>)
        =
        match continuation with
        | Ok _ -> ()
        | Error _ -> notifyFailure eventPort turn error

    let rec private sendStagedBloggerContinuation
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (ctx: BloggerRequestContext)
        (prompt: string)
        (failureReason: string)
        : Task =
        task {
            let! outcome =
                taskResult {
                    do!
                        BloggerCoordinator.materializeContinuationContext scope durable ctx
                        |> TaskResult.mapError BloggerContinuationFailure.Materialize

                    let! promptKey =
                        sendContinuation sessionPort turn (Some durable) prompt
                        |> TaskResult.mapError BloggerContinuationFailure.Send

                    do!
                        BloggerCoordinator.bindContinuationContext scope durable ctx promptKey
                        |> TaskResult.mapError BloggerContinuationFailure.Bind
                }

            return! settleBloggerContinuationFailure eventPort durable scope turn ctx failureReason outcome
        }

    and private settleBloggerContinuationFailure
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (ctx: BloggerRequestContext)
        (failureReason: string)
        outcome
        : Task =
        task {
            match outcome with
            | Ok() -> ()
            | Error(BloggerContinuationFailure.Materialize reason) -> notifyFailure eventPort turn reason
            | Error(BloggerContinuationFailure.Send reason) ->
                do! BloggerCoordinator.abandonContinuationContext scope durable ctx reason
                notifyFailure eventPort turn failureReason
            | Error(BloggerContinuationFailure.Bind reason) ->
                do! BloggerCoordinator.abandonContinuationContext scope durable ctx reason
                notifyFailure eventPort turn reason
        }

    let private replaceFailedBloggerRequest
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (failed: BloggerRequestContext)
        (next: BloggerRequestContext)
        (prompt: string)
        (failureReason: string)
        : Task =
        task {
            do! BloggerCoordinator.abandonContinuationContext scope durable failed "provider-attempt-failed"

            return! sendStagedBloggerContinuation sessionPort eventPort durable scope turn next prompt failureReason
        }

    let private continueFailedBloggerMain
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (mainSessionId: SessionId)
        (continuationPrompt: string)
        (error: string)
        (opportunity: RecoveryOpportunity)
        (squash: BloggerRequestContext option)
        (failed: BloggerRequestContext)
        : Task =
        match RecoverySlot.nextBloggerRequest ProviderRequestKind.BloggerMain opportunity squash.IsSome, squash with
        | Ok ProviderRequestKind.BloggerSquash, Some squashCtx ->
            replaceFailedBloggerRequest
                sessionPort
                eventPort
                durable
                scope
                turn
                failed
                squashCtx
                (squashPrompt mainSessionId)
                error
        | Ok ProviderRequestKind.BloggerMain, _ ->
            replaceFailedBloggerRequest sessionPort eventPort durable scope turn failed failed continuationPrompt error
        | Ok _, _
        | Error _, _ ->
            notifyFailure eventPort turn "Blogger recovery produced an invalid next request kind"
            Task.FromResult(()) :> Task

    let private rebuildMainAfterFailedSquash
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (mainSessionId: SessionId)
        (continuationPrompt: string)
        (error: string)
        (failed: BloggerRequestContext)
        : Task =
        task {
            match! BloggerMainContext.fromJournal scope durable mainSessionId turn.SessionId with
            | None -> notifyFailure eventPort turn "Blogger squash failed and no main material can be rebuilt"
            | Some main ->
                return!
                    replaceFailedBloggerRequest
                        sessionPort
                        eventPort
                        durable
                        scope
                        turn
                        failed
                        main
                        continuationPrompt
                        error
        }

    let private continueFailedBloggerSquash
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (mainSessionId: SessionId)
        (continuationPrompt: string)
        (error: string)
        (opportunity: RecoveryOpportunity)
        (squash: BloggerRequestContext option)
        (failed: BloggerRequestContext)
        : Task =
        match RecoverySlot.nextBloggerRequest ProviderRequestKind.BloggerSquash opportunity squash.IsSome with
        | Error _ ->
            notifyFailure eventPort turn "Blogger squash recovery produced an invalid next request kind"
            Task.FromResult(()) :> Task
        | Ok ProviderRequestKind.BloggerMain ->
            rebuildMainAfterFailedSquash
                sessionPort
                eventPort
                durable
                scope
                turn
                mainSessionId
                continuationPrompt
                error
                failed
        | Ok _ ->
            notifyFailure eventPort turn "Blogger squash recovery did not return to main"
            Task.FromResult(()) :> Task

    let private continueBlogger
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (mainSessionId: SessionId)
        (continuationPrompt: string)
        (error: string)
        (opportunity: RecoveryOpportunity)
        : Task =
        task {
            let current = scope.TryPeekCurrentRequest(SessionId.value turn.SessionId)
            let squash = recoverySquashContext durable mainSessionId turn.SessionId

            match current with
            | None -> notifyFailure eventPort turn "Blogger recovery has no owned request context"
            | Some((BloggerRequestContext.Main _) as failed) ->
                return!
                    continueFailedBloggerMain
                        sessionPort
                        eventPort
                        durable
                        scope
                        turn
                        mainSessionId
                        continuationPrompt
                        error
                        opportunity
                        squash
                        failed
            | Some((BloggerRequestContext.Squash _) as failed) ->
                return!
                    continueFailedBloggerSquash
                        sessionPort
                        eventPort
                        durable
                        scope
                        turn
                        mainSessionId
                        continuationPrompt
                        error
                        opportunity
                        squash
                        failed
        }

    let private continueWorkMain
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (continuationPrompt: string)
        (error: string)
        (opportunity: RecoveryOpportunity)
        : Task =
        task {
            if opportunity = RecoveryOpportunity.RecoveryAttempt then
                do! awaitRecoveryMaterial scope durable turn.SessionId

            let! continuation = sendContinuation sessionPort turn (Some durable) continuationPrompt
            handleContinuation eventPort turn error continuation
        }

    let private continueAdvancedFailure
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (continuationPrompt: string)
        (error: string)
        (opportunity: RecoveryOpportunity)
        : Task =
        match mainSessionOfBlogger durable turn.SessionId with
        | Some mainSessionId ->
            continueBlogger sessionPort eventPort durable scope turn mainSessionId continuationPrompt error opportunity
        | None -> continueWorkMain sessionPort eventPort durable scope turn continuationPrompt error opportunity

    let private settleFailureAdmission
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (continuationPrompt: string)
        (error: string)
        admission
        : Task =
        match admission with
        | Error reason ->
            notifyFailure eventPort turn reason
            Task.FromResult(()) :> Task
        | Ok ConfirmedFailureOutcome.RecoveryExhausted ->
            notifyFailure eventPort turn error
            Task.FromResult(()) :> Task
        | Ok ConfirmedFailureOutcome.AlreadyRecorded -> Task.FromResult(()) :> Task
        | Ok ConfirmedFailureOutcome.NoActiveRun ->
            notifyFailure eventPort turn "Confirmed provider failure has no active fallback run"
            Task.FromResult(()) :> Task
        | Ok(ConfirmedFailureOutcome.RecoveryAdvanced opportunity) ->
            continueAdvancedFailure sessionPort eventPort durable scope turn continuationPrompt error opportunity

    let private fallbackBudgetOf (current: FallbackProjection) =
        if FallbackProjection.mayContinue AgentPairCursor.DefaultAutoRecoveryBudget current then
            ProviderRecoveryBudget.Available
        else
            ProviderRecoveryBudget.Exhausted

    let private recoveryDecision
        (turn: ReconciledTurn)
        (failure: ExecutionFailure)
        (current: FallbackProjection)
        (requestKind: ProviderRequestKind)
        =
        ExecutionFailurePolicy.decide
            { Failure = failure
              Lifecycle = DurableExecutionLifecycle.ProviderStarted
              ExecutionKey =
                { SessionId = turn.SessionId
                  PhysicalUserMessageId = turn.PhysicalUserMessageId }
              Capacity = CapacityOwnership.NoCapacityFence
              Provider =
                { LogicalRun = current.LogicalRunId
                  ProviderRun = turn.ProviderRun
                  RequestKind = requestKind
                  RetryBudget = ProviderRecoveryBudget.Exhausted
                  FallbackBudget = fallbackBudgetOf current
                  Breaker = ProviderBreakerState.Closed } }

    [<RequireQualifiedAccess>]
    type private PolicyFallbackDecision =
        | Exhausted
        | Authorized of ProviderRecoveryAuthorization

    let private policyFallbackDecision
        (turn: ReconciledTurn)
        (failure: ExecutionFailure)
        (current: FallbackProjection)
        (requestKind: ProviderRequestKind)
        =
        match (recoveryDecision turn failure current requestKind).Fallback with
        | FallbackDecision.NoFallback -> PolicyFallbackDecision.Exhausted
        | FallbackDecision.AdvanceFallback authorization -> PolicyFallbackDecision.Authorized authorization

    let private outcomeAfterRepeatedAdmission (durable: AgentJournal) (sessionId: SessionId) =
        match FallbackEvidence.tryCurrentState sessionId (AgentJournal.snapshot durable) with
        | Some latest when fallbackBudgetOf latest = ProviderRecoveryBudget.Exhausted ->
            Ok ConfirmedFailureOutcome.RecoveryExhausted
        | _ -> Ok ConfirmedFailureOutcome.AlreadyRecorded

    let private reconcileFailureAdmission (durable: AgentJournal) (sessionId: SessionId) admission =
        match admission with
        | Ok ConfirmedFailureOutcome.AlreadyRecorded -> outcomeAfterRepeatedAdmission durable sessionId
        | _ -> admission

    let private admitAuthorizedFailure
        (durable: AgentJournal)
        (ownerSessionId: SessionId)
        (turn: ReconciledTurn)
        (authorization: ProviderRecoveryAuthorization)
        (error: string)
        =
        task {
            let! admission = FallbackLedger.recordAuthorizedFailure durable ownerSessionId authorization error
            return reconcileFailureAdmission durable ownerSessionId admission
        }

    let private admitCurrentFailure
        (durable: AgentJournal)
        (ownerSessionId: SessionId)
        (turn: ReconciledTurn)
        (failure: ExecutionFailure)
        (requestKind: ProviderRequestKind)
        (error: string)
        (current: FallbackProjection)
        =
        match policyFallbackDecision turn failure current requestKind with
        | PolicyFallbackDecision.Exhausted -> Task.FromResult(Ok ConfirmedFailureOutcome.RecoveryExhausted)
        | PolicyFallbackDecision.Authorized authorization ->
            admitAuthorizedFailure durable ownerSessionId turn authorization error

    let admitPolicyAuthorizedFailure
        (durable: AgentJournal)
        (turn: ReconciledTurn)
        (failure: ExecutionFailure)
        (requestKind: ProviderRequestKind)
        (error: string)
        : Task<Result<ConfirmedFailureOutcome, string>> =
        let projection = AgentJournal.snapshot durable

        let ownerSessionId =
            mainSessionOfBloggerProjection projection turn.SessionId
            |> Option.orElseWith (fun () ->
                FallbackEvidence.tryCurrentState turn.SessionId projection
                |> Option.map (fun _ -> turn.SessionId))

        match ownerSessionId with
        | None -> Task.FromResult(Ok ConfirmedFailureOutcome.NoActiveRun)
        | Some owner ->
            FallbackEvidence.tryCurrentState owner projection
            |> Option.map (admitCurrentFailure durable owner turn failure requestKind error)
            |> Option.defaultWith (fun () -> Task.FromResult(Ok ConfirmedFailureOutcome.NoActiveRun))

    let private executeFallbackDecision
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (continuationPrompt: string)
        (error: string)
        (decision: ExecutionFailureDecision)
        : Task =
        match decision.Fallback with
        | FallbackDecision.NoFallback ->
            notifyFailure eventPort turn error
            Task.FromResult(()) :> Task
        | FallbackDecision.AdvanceFallback authorization ->
            task {
                let! admission = FallbackLedger.recordAuthorizedFailure durable turn.SessionId authorization error

                return!
                    settleFailureAdmission sessionPort eventPort durable scope turn continuationPrompt error admission
            }

    let private executeAuthorizedRecovery
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (failure: ExecutionFailure)
        (continuationPrompt: string)
        (error: string)
        (current: FallbackProjection)
        (requestKind: ProviderRequestKind)
        =
        recoveryDecision turn failure current requestKind
        |> executeFallbackDecision sessionPort eventPort durable scope turn continuationPrompt error

    let private continueDurableFailure
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (failure: ExecutionFailure)
        (continuationPrompt: string)
        (error: string)
        : Task =
        task {
            match
                FallbackEvidence.tryCurrentState turn.SessionId (AgentJournal.snapshot durable),
                requestKindFor durable scope turn.SessionId
            with
            | None, _
            | _, None -> notifyFailure eventPort turn error
            | Some current, Some requestKind ->
                return!
                    executeAuthorizedRecovery
                        sessionPort
                        eventPort
                        durable
                        scope
                        turn
                        failure
                        continuationPrompt
                        error
                        current
                        requestKind
        }

    /// FALLBACK-003 + FALLBACK-004: a settled failed turn.
    ///
    /// The reconciled snapshot is what proves the attempt failed (HOST-004), so
    /// this is where the cursor advances — not in the Host retry event handler,
    /// which only wakes. `FallbackLedger` is the Application single writer.
    ///
    /// FALLBACK-004 then decides whether a continuation follows: only when the
    /// budget still permits one. The continuation itself produces no second
    /// advance, which is why nothing here writes again.
    let continueAfterConfirmedFailure
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (failure: ExecutionFailure)
        (error: string)
        (continuationPrompt: string)
        : Task =
        task {
            match journal with
            | None -> notifyFailure eventPort turn error
            | Some durable ->
                return! continueDurableFailure sessionPort eventPort durable scope turn failure continuationPrompt error
        }
        :> Task
