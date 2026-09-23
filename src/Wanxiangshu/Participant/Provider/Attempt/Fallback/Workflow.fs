namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

/// Confirmed-failure recovery.
/// WorkMain retries on the next provider failure budget; BloggerMain may insert one BloggerSquash
/// maintenance request before retrying main. No future unrelated provider material is a
/// retry trigger.
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
        | Retired

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

    /// CTX-023 / provider-attempt-recovery-018: durable event wait. The open materialization is the
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

    let private mainSessionOfBlogger (durable: AgentJournal) (bloggerSessionId: SessionId) =
        SessionAssociationProjection.tryMainSessionOf
            bloggerSessionId
            (AgentJournal.snapshot durable).AgentProjections.Associations

    let private recoveryOwnerSession
        (projection: ProjectionSet)
        (failedSessionId: SessionId)
        (requestKind: ProviderRequestKind)
        =
        match requestKind with
        | ProviderRequestKind.BloggerMain
        | ProviderRequestKind.BloggerSquash ->
            SessionAssociationProjection.tryMainSessionOf failedSessionId projection.AgentProjections.Associations
        | ProviderRequestKind.WorkMain
        | ProviderRequestKind.InteractionRepair -> Some failedSessionId
        | ProviderRequestKind.StrengthReplica -> None

    /// provider-attempt-recovery-003 / provider-attempt-recovery-023: the request kind a confirmed
    /// failure continues with.
    ///
    /// The durable `ProviderStarted` fact is the primary evidence. An execution
    /// that is durably `Accepted` without it (the provider-attempt-recovery-023 shape) still
    /// names its ordinary kind through the accepted evidence origin, so a
    /// confirmed failure of such an attempt is retried instead of being reported
    /// as an unretryable terminal. Satellite (Blogger) sessions keep requiring
    /// the persisted start: their kind comes from the open typed request, and a
    /// guess would cross the ownership line.
    let requestKindFor
        (durable: AgentJournal)
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        : ProviderRequestKind option =
        let projection = AgentJournal.snapshot durable

        projection.AgentProjections.ChatExecutions
        |> ChatExecutionProjection.byKey
            { SessionId = sessionId
              PhysicalUserMessageId = physicalUserMessageId }
        |> Option.bind (fun execution ->
            match execution.ProviderStarted with
            | Some started -> Some started.RequestKind
            | None when SessionAssociationProjection.isSatellite sessionId projection.AgentProjections.Associations ->
                None
            | None -> Some(AttemptPlanner.ordinaryRequestKind execution.Evidence.Origin))

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

    let private recoveryGateKind (authorization: ProviderRecoveryAuthorization) =
        "provider-recovery:" + authorization.DecisionId.Value

    let private recoveryAlreadyAdmitted
        (durable: AgentJournal)
        (turn: ReconciledTurn)
        (authorization: ProviderRecoveryAuthorization)
        =
        match HostSessionNudge.tryActiveProfile (Some durable) turn.SessionId with
        | None -> false
        | Some profile ->
            (PromptDispatcher.forPrompts (PromptJournalAdapter.create durable))
                .GateNudgeAlreadyAdmitted
                profile
                PromptAuthority.ContinuationKind.ProviderRetryAttempt
                (recoveryGateKind authorization)
                authorization.ProviderRun

    let private sendRecoveryContinuation
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (turn: ReconciledTurn)
        (durable: AgentJournal)
        (authorization: ProviderRecoveryAuthorization)
        (prompt: string)
        =
        HostSessionNudge.trySendGateContinuation
            sessionPort
            rootWorkspace
            turn.SessionId
            prompt
            PromptAuthority.ContinuationKind.ProviderRetryAttempt
            turn.Directory
            (Some durable)
            (recoveryGateKind authorization)
            authorization.ProviderRun

    let private bloggerPromptKey =
        function
        | HostSessionNudge.GateContinuationOutcome.Sent key -> Ok(Some key)
        | HostSessionNudge.GateContinuationOutcome.AlreadyAdmitted -> Ok None
        | HostSessionNudge.GateContinuationOutcome.Retired -> Error BloggerContinuationFailure.Retired
        | HostSessionNudge.GateContinuationOutcome.Failed error -> Error(BloggerContinuationFailure.Send error)

    let private bindBloggerPrompt
        (scope: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (ctx: BloggerRequestContext)
        promptKey
        =
        match promptKey with
        | None -> Task.FromResult(Ok())
        | Some key ->
            BloggerCoordinator.bindContinuationContext scope durable ctx key
            |> TaskResult.mapError BloggerContinuationFailure.Bind

    let rec private sendStagedBloggerContinuation
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (authorization: ProviderRecoveryAuthorization)
        (ctx: BloggerRequestContext)
        (prompt: string)
        (failureReason: string)
        : Task<RetryVerdict> =
        task {
            let! outcome =
                taskResult {
                    do!
                        BloggerCoordinator.materializeContinuationContext scope durable ctx
                        |> TaskResult.mapError BloggerContinuationFailure.Materialize

                    let! promptKey =
                        sendRecoveryContinuation sessionPort rootWorkspace turn durable authorization prompt
                        |> TaskValue.map bloggerPromptKey

                    do! bindBloggerPrompt scope durable ctx promptKey
                }

            match outcome with
            | Ok() -> return RetryVerdict.Dispatched
            | Error(BloggerContinuationFailure.Materialize reason) -> return RetryVerdict.Terminal reason
            | Error(BloggerContinuationFailure.Send reason) ->
                do! BloggerCoordinator.abandonContinuationContext scope durable ctx reason
                return RetryVerdict.Terminal failureReason
            | Error(BloggerContinuationFailure.Bind reason) ->
                do! BloggerCoordinator.abandonContinuationContext scope durable ctx reason
                return RetryVerdict.Terminal reason
            | Error BloggerContinuationFailure.Retired ->
                do! BloggerCoordinator.abandonContinuationContext scope durable ctx "recovery target retired"
                return RetryVerdict.Superseded
        }

    let private replaceFailedBloggerRequest
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (authorization: ProviderRecoveryAuthorization)
        (failed: BloggerRequestContext)
        (next: BloggerRequestContext)
        (prompt: string)
        (failureReason: string)
        : Task<RetryVerdict> =
        task {
            do! BloggerCoordinator.abandonContinuationContext scope durable failed "provider-attempt-failed"

            return!
                sendStagedBloggerContinuation
                    sessionPort
                    rootWorkspace
                    durable
                    scope
                    turn
                    authorization
                    next
                    prompt
                    failureReason
        }

    let private continueFailedBloggerMain
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (authorization: ProviderRecoveryAuthorization)
        (mainSessionId: SessionId)
        (continuationPrompt: string)
        (error: string)
        (squash: BloggerRequestContext option)
        (failed: BloggerRequestContext)
        : Task<RetryVerdict> =
        match BloggerRetryPolicy.nextRequest ProviderRequestKind.BloggerMain squash.IsSome, squash with
        | Ok ProviderRequestKind.BloggerSquash, Some squashCtx ->
            replaceFailedBloggerRequest
                sessionPort
                rootWorkspace
                durable
                scope
                turn
                authorization
                failed
                squashCtx
                (squashPrompt mainSessionId)
                error
        | Ok ProviderRequestKind.BloggerMain, _ ->
            replaceFailedBloggerRequest
                sessionPort
                rootWorkspace
                durable
                scope
                turn
                authorization
                failed
                failed
                continuationPrompt
                error
        | Ok _, _
        | Error _, _ -> Task.FromResult(RetryVerdict.Terminal "Blogger recovery produced an invalid next request kind")

    let private rebuildMainAfterFailedSquash
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (authorization: ProviderRecoveryAuthorization)
        (mainSessionId: SessionId)
        (continuationPrompt: string)
        (error: string)
        (failed: BloggerRequestContext)
        : Task<RetryVerdict> =
        task {
            match! BloggerMainContext.fromJournal scope durable mainSessionId turn.SessionId with
            | None -> return RetryVerdict.Terminal "Blogger squash failed and no main material can be rebuilt"
            | Some main ->
                return!
                    replaceFailedBloggerRequest
                        sessionPort
                        rootWorkspace
                        durable
                        scope
                        turn
                        authorization
                        failed
                        main
                        continuationPrompt
                        error
        }

    let private continueFailedBloggerSquash
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (authorization: ProviderRecoveryAuthorization)
        (mainSessionId: SessionId)
        (continuationPrompt: string)
        (error: string)
        (squash: BloggerRequestContext option)
        (failed: BloggerRequestContext)
        : Task<RetryVerdict> =
        match BloggerRetryPolicy.nextRequest ProviderRequestKind.BloggerSquash squash.IsSome with
        | Error _ ->
            Task.FromResult(RetryVerdict.Terminal "Blogger squash recovery produced an invalid next request kind")
        | Ok ProviderRequestKind.BloggerMain ->
            rebuildMainAfterFailedSquash
                sessionPort
                rootWorkspace
                durable
                scope
                turn
                authorization
                mainSessionId
                continuationPrompt
                error
                failed
        | Ok _ -> Task.FromResult(RetryVerdict.Terminal "Blogger squash recovery did not return to main")

    let private continueBlogger
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (authorization: ProviderRecoveryAuthorization)
        (mainSessionId: SessionId)
        (continuationPrompt: string)
        (error: string)
        : Task<RetryVerdict> =
        task {
            let current = scope.TryPeekCurrentRequest(SessionId.value turn.SessionId)
            let squash = recoverySquashContext durable mainSessionId turn.SessionId

            match current with
            | None -> return RetryVerdict.Terminal "Blogger recovery has no owned request context"
            | Some((BloggerRequestContext.Main _) as failed) ->
                return!
                    continueFailedBloggerMain
                        sessionPort
                        rootWorkspace
                        durable
                        scope
                        turn
                        authorization
                        mainSessionId
                        continuationPrompt
                        error
                        squash
                        failed
            | Some((BloggerRequestContext.Squash _) as failed) ->
                return!
                    continueFailedBloggerSquash
                        sessionPort
                        rootWorkspace
                        durable
                        scope
                        turn
                        authorization
                        mainSessionId
                        continuationPrompt
                        error
                        squash
                        failed
        }

    let private continueWorkMain
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (authorization: ProviderRecoveryAuthorization)
        (continuationPrompt: string)
        (error: string)
        : Task<RetryVerdict> =
        task {
            do! awaitRecoveryMaterial scope durable turn.SessionId

            let! continuation =
                sendRecoveryContinuation sessionPort rootWorkspace turn durable authorization continuationPrompt

            return
                match continuation with
                | HostSessionNudge.GateContinuationOutcome.Sent _
                | HostSessionNudge.GateContinuationOutcome.AlreadyAdmitted -> RetryVerdict.Dispatched
                | HostSessionNudge.GateContinuationOutcome.Retired -> RetryVerdict.Superseded
                | HostSessionNudge.GateContinuationOutcome.Failed _ -> RetryVerdict.Terminal error
        }

    /// provider-attempt-recovery-021: the two durable facts the recovery target settlement consumes.
    /// The failed attempt was the LWR retry iff its exact physical request was
    /// accepted as a `ProviderRetryAttempt` continuation AND the failed provider
    /// run is the exact run that established that request's durable
    /// `ProviderStarted`. One physical message drives several provider steps, so
    /// the continuation kind alone is not per-attempt: after an LWR retry's first
    /// step succeeds, a later step of the same message would otherwise wrongly
    /// condemn the provider on its first failure. Never inferred from a failure
    /// ordinal.
    let failedAttemptWasLwrRetry
        (durable: AgentJournal)
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (providerRun: ProviderRunIdentity)
        : bool =
        let kind =
            (AgentJournalPortAdapter.forTurnObservation durable).TryContinuationKind sessionId physicalUserMessageId

        let establishedRun =
            (AgentJournal.snapshot durable).AgentProjections.ChatExecutions
            |> ChatExecutionProjection.byKey
                { SessionId = sessionId
                  PhysicalUserMessageId = physicalUserMessageId }
            |> Option.bind (fun execution -> execution.ProviderStarted)
            |> Option.map (fun started -> started.ProviderRun)

        kind = Some PromptAuthority.ContinuationKind.ProviderRetryAttempt
        && establishedRun = Some providerRun

    /// provider-attempt-recovery-021: the single target settlement for one confirmed provider failure,
    /// shared by ordinary recovery, the SyncDelegate decorator and recovery
    /// re-entry. A failed LWR retry condemns its provider; every other failure
    /// keeps the provider and binds the next fresh admission of this session to
    /// the failed target for the LWR retry.
    let private settleFailedAttemptTarget (durable: AgentJournal) (turn: ReconciledTurn) =
        if failedAttemptWasLwrRetry durable turn.SessionId turn.PhysicalUserMessageId turn.ProviderRun then
            ModelRouting.condemnFailedTarget turn.ProviderRun |> ignore
        else
            ModelRouting.retainFailedTargetForRetry turn.SessionId turn.ProviderRun
            |> ignore

    /// Path plug of the retry decorator: perform this path's physical re-entry
    /// for one licensed attempt and report the verdict.
    let rec private redispatchAfterFailure
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (continuationPrompt: string)
        (failureReason: string)
        (authorization: ProviderRecoveryAuthorization)
        (input: RetryAttempt)
        : Task<RetryVerdict> =
        task {
            let admitted = recoveryAlreadyAdmitted durable input.Turn authorization

            // provider-attempt-recovery-021: only a licensed, not-yet-dispatched redispatch settles
            // the failed target; the witness and the durable prompt claim each
            // hold exactly one permission.
            if not admitted then
                settleFailedAttemptTarget durable input.Turn

            // provider-attempt-recovery-022: a recovery continuation may only be sent after the Host
            // stopped automatic retry for this exact provider run. A send inside
            // the still-running failed turn is silently dropped by the Host
            // (`SessionRunState.ensureRunning` joins the dying run instead of
            // starting the new turn), so the fence — not this workflow — owns
            // the dispatch occasion.
            let! hostStopped = ProviderAttemptStopFence.shared.AwaitStop(input.Turn.SessionId, input.Turn.ProviderRun)

            match admitted, hostStopped, input.RequestKind with
            | true, _, _ -> return RetryVerdict.Superseded
            | false, false, _ -> return RetryVerdict.Superseded
            | false, true, (ProviderRequestKind.BloggerMain | ProviderRequestKind.BloggerSquash) ->
                return!
                    dispatchBloggerAfterFailure
                        sessionPort
                        rootWorkspace
                        durable
                        scope
                        continuationPrompt
                        failureReason
                        authorization
                        input
            | false, true, (ProviderRequestKind.WorkMain | ProviderRequestKind.InteractionRepair) ->
                return!
                    continueWorkMain
                        sessionPort
                        rootWorkspace
                        durable
                        scope
                        input.Turn
                        authorization
                        continuationPrompt
                        failureReason
            | false, true, ProviderRequestKind.StrengthReplica ->
                // The policy never licenses a replica recovery; fail closed.
                return RetryVerdict.Superseded
        }

    and dispatchBloggerAfterFailure
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (continuationPrompt: string)
        (failureReason: string)
        (authorization: ProviderRecoveryAuthorization)
        (input: RetryAttempt)
        : Task<RetryVerdict> =
        task {
            match mainSessionOfBlogger durable input.Turn.SessionId with
            | None -> return RetryVerdict.Terminal "Confirmed Blogger provider failure has no linked main session"
            | Some mainSessionId ->
                return!
                    continueBlogger
                        sessionPort
                        rootWorkspace
                        durable
                        scope
                        input.Turn
                        authorization
                        mainSessionId
                        continuationPrompt
                        failureReason
        }

    let private admitAuthorizedFailure
        (durable: AgentJournal)
        (ownerSessionId: SessionId)
        (authorization: ProviderRecoveryAuthorization)
        (error: string)
        =
        let port =
            Wanxiangshu.Composition.Durable.AgentJournalPortAdapter.forProviderFailure durable

        ProviderFailureLedger.recordAuthorizedFailure port ownerSessionId authorization error

    let rec private continueDurableFailure
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (failure: ExecutionFailure)
        (continuationPrompt: string)
        (error: string)
        : Task =
        task {
            let projections = (AgentJournal.snapshot durable)

            let activeProfileOpt =
                PromptAuthorityProjectionQueries.activeProfile turn.SessionId projections.AgentProjections

            let roleName =
                activeProfileOpt
                |> Option.map (fun profile -> Roles.roleLabel profile.CanonicalRole)
                |> Option.defaultValue ""

            let hasCapacity = roleName = "" || ModelRouting.hasTheoreticalCapacity roleName

            let recoveryContext =
                requestKindFor durable turn.SessionId turn.PhysicalUserMessageId
                |> Option.bind (fun requestKind ->
                    recoveryOwnerSession projections turn.SessionId requestKind
                    |> Option.bind (fun ownerSessionId ->
                        let failureState =
                            AgentProjection.tryFind ownerSessionId projections.AgentProjections
                            |> Option.bind _.ProviderFailures

                        ProviderFailureEvidence.currentState failureState
                        |> Option.map (fun current -> ownerSessionId, requestKind, current)))
                |> Option.orElseWith (fun () ->
                    // If the first turn/opening failed before ProviderStarted was durable,
                    // fall back to default WorkMain under the active authority profile.
                    activeProfileOpt
                    |> Option.bind (fun _ ->
                        let ownerSessionId = turn.SessionId
                        let requestKind = ProviderRequestKind.WorkMain

                        let failureState =
                            AgentProjection.tryFind ownerSessionId projections.AgentProjections
                            |> Option.bind _.ProviderFailures

                        ProviderFailureEvidence.currentState failureState
                        |> Option.map (fun current -> ownerSessionId, requestKind, current)))

            match hasCapacity, recoveryContext with
            | false, _ -> notifyFailure eventPort turn "All candidate providers exhausted (zero capacity)"
            | true, None -> notifyFailure eventPort turn error
            | true, Some(ownerSessionId, requestKind, current) ->
                return!
                    retryConfirmedFailure
                        sessionPort
                        rootWorkspace
                        eventPort
                        durable
                        scope
                        turn
                        failure
                        continuationPrompt
                        error
                        ownerSessionId
                        requestKind
                        current
        }

    and retryConfirmedFailure
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (scope: IBloggerRuntimeHost)
        (turn: ReconciledTurn)
        (failure: ExecutionFailure)
        (continuationPrompt: string)
        (error: string)
        (ownerSessionId: SessionId)
        (requestKind: ProviderRequestKind)
        (current: ProviderFailureProjection)
        : Task =
        task {
            let! verdict =
                Retry.attempt
                    { Admit = fun authorization -> admitAuthorizedFailure durable ownerSessionId authorization error
                      Redispatch =
                        redispatchAfterFailure sessionPort rootWorkspace durable scope continuationPrompt error }
                    { Turn = turn
                      Failure = failure
                      OwnerSession = ownerSessionId
                      RequestKind = requestKind
                      Current = current
                      Error = error }

            match verdict with
            | RetryVerdict.Dispatched
            | RetryVerdict.Superseded -> ()
            | RetryVerdict.Terminal reason -> notifyFailure eventPort turn reason
        }
        :> Task

    /// Confirmed provider failure handling.
    ///
    /// The reconciled snapshot is what proves the attempt failed (HOST-004), so
    /// this is where the failure budget is recorded — not in the Host retry event
    /// handler, which only wakes. `ProviderFailureLedger` is the Application single writer.
    ///
    /// Settle admission then decides whether a continuation follows: only when the
    /// budget still permits retry.
    let continueAfterConfirmedFailure
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
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
                return!
                    continueDurableFailure
                        sessionPort
                        rootWorkspace
                        eventPort
                        durable
                        scope
                        turn
                        failure
                        continuationPrompt
                        error
        }
        :> Task

    /// Retry-decorator plug for one dedicated delegate child (delegation-023).
    ///
    /// The caller observes only the verdict: a single transient attempt failure
    /// never reaches it. Recovery reuses the same policy, ledger and WorkMain
    /// redispatch as the ordinary turn path.
    let continueDelegateCallAfterConfirmedFailure
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (scope: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (turn: ReconciledTurn)
        (failure: ExecutionFailure)
        (error: string)
        : Task<RetryVerdict> =
        task {
            let projections = AgentJournal.snapshot durable

            let roleName =
                PromptAuthorityProjectionQueries.activeProfile turn.SessionId projections.AgentProjections
                |> Option.map (fun profile -> Roles.roleLabel profile.CanonicalRole)
                |> Option.defaultValue ""

            let hasCapacity = roleName = "" || ModelRouting.hasTheoreticalCapacity roleName

            let recoveryContext =
                requestKindFor durable turn.SessionId turn.PhysicalUserMessageId
                |> Option.bind (fun requestKind ->
                    recoveryOwnerSession projections turn.SessionId requestKind
                    |> Option.bind (fun ownerSessionId ->
                        let failureState =
                            AgentProjection.tryFind ownerSessionId projections.AgentProjections
                            |> Option.bind _.ProviderFailures

                        ProviderFailureEvidence.currentState failureState
                        |> Option.map (fun current -> ownerSessionId, requestKind, current)))

            match hasCapacity, recoveryContext with
            | false, _ -> return RetryVerdict.Terminal "All candidate providers exhausted (zero capacity)"
            | true, None -> return RetryVerdict.Terminal error
            | true, Some(ownerSessionId, requestKind, current) ->
                return!
                    Retry.attempt
                        { Admit = fun authorization -> admitAuthorizedFailure durable ownerSessionId authorization error
                          Redispatch =
                            redispatchAfterFailure
                                sessionPort
                                rootWorkspace
                                durable
                                scope
                                (ProviderProse.documentFor turn.SessionId RuntimeNudge.ProviderRetry Map.empty)
                                error }
                        { Turn = turn
                          Failure = failure
                          OwnerSession = ownerSessionId
                          RequestKind = requestKind
                          Current = current
                          Error = error }
        }
