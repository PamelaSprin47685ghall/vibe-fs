namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

/// ENFORCER-047/050: the ONE main-session decision entry for Blogger material.
module BloggerCoordinator =

    /// R04: one process-local repair owner CE per exact live request/authority.
    /// Identity is exact RequestId + durable AuthorityRoot + Main/Blogger session ids.
    /// Transform and idle observations post to the shared rendezvous mailbox;
    /// the single CE lexically performs at most one nudge then at most one AABB
    /// then abandon. No journal fact chooses the next repair action.
    /// Small typed reader adapting the rendezvous function port back to the
    /// nudge-send contract. No boxing, no unchecked cast.
    type private RootWorkspaceFunctionReader(tryRead: unit -> string option) =
        interface IRootWorkspaceReader with
            member _.TryRead() = tryRead ()

    /// Authority comes only from the current durable Blogger profile. No fake root.
    let private activeAuthorityRoot
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        : AuthorityRootUserMessageId option =
        (AgentJournal.snapshot journal).AgentProjections
        |> PromptAuthorityProjectionQueries.activeProfile bloggerSessionId
        |> Option.map (fun profile -> profile.AuthorityRootUserMessageId)

    /// Exact live flight ownership: the flight registry must hold this exact request.
    let private exactFlightMatches (scope: IBloggerRuntimeHost) (request: BloggerRequestContext) : bool =
        let key = SessionId.value (BloggerRequestContext.bloggerSessionId request)

        match scope.TryGetFlight key with
        | Some flight -> BloggerRequestContext.requestId flight = BloggerRequestContext.requestId request
        | None -> false

    /// Exhaustion / Failed / Retired unwind: durable abandon, exact-request release
    /// (conflict fail-closed via the exact wrapper), then terminal signal on the
    /// available/stored event port.
    let private abandonEpisode
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal)
        (request: BloggerRequestContext)
        (identity: BloggerRepairEpisodeIdentity)
        (eventPort: IEventObservationPort option)
        (reason: string)
        : Task =
        task {
            do!
                BloggerAbandon.openRequest
                    journal
                    (BloggerRequestContext.mainSessionId request)
                    (BloggerRequestContext.bloggerSessionId request)
                    (Some request)
                    reason

            match eventPort with
            | Some port ->
                port.NotifyTerminal
                    identity.BloggerSessionId
                    (TerminalOutcome.Failed(TerminalStop.forAuthority identity.AuthorityRoot reason))
                |> ignore
            | None -> ()

            match
                BloggerRuntimeHost.releaseCurrentRequest scope (SessionId.value identity.BloggerSessionId) request
            with
            | Ok() -> ()
            | Error releaseErr -> FatalProcess.trip "blogger-flight-release-conflict" releaseErr
        }

    let claimFlightLease
        (scope: IBloggerRuntimeHost)
        (ctx: BloggerRequestContext)
        : Result<IBloggerFlightLease, string> =
        let key = SessionId.value (BloggerRequestContext.bloggerSessionId ctx)
        BloggerRuntimeHost.claimFlight scope key ctx

    /// The ONE repair owner CE for an exact live request/authority episode.
    /// Lexical budget: idle nudge on the first quiescent T0, exactly one AABB on the
    /// first new invalid T1 (transform injection or idle send, whichever arrives first),
    /// abandon + NotifyTerminal on the next new invalid T2. Same-run duplicates and
    /// non-quiescent idles reply Pending and never advance. Cancel wakes the receiver
    /// and every reply; Completion is always observable. Crash loses the episode and
    /// never reconstructs it: no journal fact is read for sequencing here.
    let private runRepairEpisode
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal)
        (identity: BloggerRepairEpisodeIdentity)
        (request: BloggerRequestContext)
        (rendezvous: BloggerRepairRendezvous)
        : Task =
        let requestKey = BloggerRequestId.value identity.RequestId

        let reply (envelope: BloggerRepairEnvelope) (outcome: BloggerRepairOutcome) =
            rendezvous.Resolve(envelope, outcome)

        let abandonAndResolve envelope eventPort reason : Task =
            task {
                try
                    do! abandonEpisode scope journal request identity eventPort reason
                    reply envelope BloggerRepairOutcome.AbandonedExhausted
                with ex ->
                    reply envelope BloggerRepairOutcome.AbandonedExhausted
                    FatalProcess.trip "blogger-repair-abandon-failed" ex.Message
            }

        let tryNudgePhysical ports permit run : Task<Result<HostSessionNudge.IdleContinuationOutcome, string>> =
            task {
                try
                    let! outcome =
                        HostSessionNudge.trySendIdleInteractionRepair
                            ports.Quiescence
                            permit
                            ports.SessionPort
                            (RootWorkspaceFunctionReader ports.TryReadRootWorkspace :> IRootWorkspaceReader)
                            ports.Context.Turn.SessionId
                            EnforcerRepair.RepairInstruction
                            ports.Context.Turn.Directory
                            (Some journal)
                            identity.RequestId
                            run
                            BloggerRecoveryProbe.BloggerMissingToolRepairKind

                    return Ok outcome
                with ex ->
                    return Error ex.Message
            }

        let tryAabbPhysical ports run : Task<Result<InteractionRepairSendOutcome, string>> =
            task {
                try
                    let! outcome =
                        HostSessionNudge.trySendInteractionRepair
                            ports.SessionPort
                            (RootWorkspaceFunctionReader ports.TryReadRootWorkspace :> IRootWorkspaceReader)
                            ports.Context.Turn.SessionId
                            EnforcerRepair.RepairInstruction
                            ports.Context.Turn.Directory
                            (Some journal)
                            identity.RequestId
                            run
                            BloggerRecoveryProbe.BloggerAabbRepairKind

                    return Ok outcome
                with ex ->
                    return Error ex.Message
            }

        let rec awaitNudge eventPort : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                let! envelopeOpt = rendezvous.Receive()

                match envelopeOpt with
                | None -> return None
                | Some envelope -> return! settleNudgeEnvelope envelope eventPort
            }

        and settleNudgeEnvelope envelope eventPort : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                try
                    return! handleNudgeEnvelope envelope eventPort
                with ex ->
                    do! abandonAndResolve envelope eventPort ("blogger repair nudge faulted: " + ex.Message)
                    return None
            }

        and handleNudgeEnvelope envelope eventPort : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                match envelope.Observation with
                | BloggerRepairObservation.TransformToolFacts _ ->
                    reply envelope BloggerRepairOutcome.PendingRepairWait
                    return! awaitNudge eventPort
                | BloggerRepairObservation.IdleQuiescentTurn(run, ports) ->
                    return! settleNudgeIdle run ports envelope eventPort
            }

        and settleNudgeIdle
            run
            ports
            envelope
            eventPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                let nextPort = Some ports.EventPort

                try
                    return! handleNudgeQuiescence run ports envelope nextPort
                with ex ->
                    do! abandonAndResolve envelope nextPort ("blogger repair nudge faulted: " + ex.Message)
                    return None
            }

        and handleNudgeQuiescence
            run
            ports
            envelope
            nextPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                match ports.Context.Quiescence with
                | None ->
                    reply envelope BloggerRepairOutcome.PendingRepairWait
                    return! awaitNudge nextPort
                | Some permit -> return! sendNudgeAfterPermit run ports permit envelope nextPort
            }

        and sendNudgeAfterPermit
            run
            ports
            permit
            envelope
            nextPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                let! physical = tryNudgePhysical ports permit run

                match physical with
                | Error msg ->
                    do! abandonAndResolve envelope nextPort ("blogger repair nudge faulted: " + msg)
                    return None
                | Ok outcome -> return! decideNudgeOutcome outcome run envelope nextPort
            }

        and decideNudgeOutcome
            outcome
            run
            envelope
            nextPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                match outcome with
                | HostSessionNudge.IdleContinuationOutcome.Sent promptKey ->
                    reply envelope (BloggerRepairOutcome.NudgeSent(Some promptKey))
                    return Some(run, nextPort)
                | HostSessionNudge.IdleContinuationOutcome.AlreadyAdmitted ->
                    reply envelope (BloggerRepairOutcome.NudgeSent None)
                    return Some(run, nextPort)
                | HostSessionNudge.IdleContinuationOutcome.Retired ->
                    do! abandonAndResolve envelope nextPort "blogger repair nudge retired"
                    return None
                | HostSessionNudge.IdleContinuationOutcome.Failed error ->
                    do! abandonAndResolve envelope nextPort ("blogger repair nudge failed: " + error)
                    return None
                | HostSessionNudge.IdleContinuationOutcome.AdmissionRejected _
                | HostSessionNudge.IdleContinuationOutcome.NotSent _ ->
                    reply envelope BloggerRepairOutcome.PendingRepairWait
                    return! awaitNudge nextPort
            }

        let rec awaitAabb nudgeRun eventPort : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                let! envelopeOpt = rendezvous.Receive()

                match envelopeOpt with
                | None -> return None
                | Some envelope -> return! settleAabbEnvelope nudgeRun envelope eventPort
            }

        and settleAabbEnvelope
            nudgeRun
            envelope
            eventPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                try
                    return! handleAabbEnvelope nudgeRun envelope eventPort
                with ex ->
                    do! abandonAndResolve envelope eventPort ("blogger AABB repair faulted: " + ex.Message)
                    return None
            }

        and handleAabbEnvelope
            nudgeRun
            envelope
            eventPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                match envelope.Observation with
                | BloggerRepairObservation.TransformToolFacts(run, rawMessages) ->
                    return! handleAabbTransform nudgeRun run rawMessages envelope eventPort
                | BloggerRepairObservation.IdleQuiescentTurn(run, ports) ->
                    return! settleAabbIdle nudgeRun run ports envelope eventPort
            }

        and handleAabbTransform
            nudgeRun
            run
            rawMessages
            envelope
            eventPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                match run = nudgeRun with
                | true ->
                    reply envelope BloggerRepairOutcome.PendingRepairWait
                    return! awaitAabb nudgeRun eventPort
                | false ->
                    let injected = EnforcerRepair.withRepairInstruction rawMessages requestKey run
                    reply envelope (BloggerRepairOutcome.RepairInjected injected)
                    return Some(run, eventPort)
            }

        and settleAabbIdle
            nudgeRun
            run
            ports
            envelope
            eventPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                let nextPort = Some ports.EventPort

                try
                    return! handleAabbIdleDuplicate nudgeRun run ports envelope nextPort
                with ex ->
                    do! abandonAndResolve envelope nextPort ("blogger AABB repair faulted: " + ex.Message)
                    return None
            }

        and handleAabbIdleDuplicate
            nudgeRun
            run
            ports
            envelope
            nextPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                match run = nudgeRun with
                | true ->
                    reply envelope BloggerRepairOutcome.PendingRepairWait
                    return! awaitAabb nudgeRun nextPort
                | false -> return! handleAabbQuiescence nudgeRun run ports envelope nextPort
            }

        and handleAabbQuiescence
            nudgeRun
            run
            ports
            envelope
            nextPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                match ports.Context.Quiescence with
                | None ->
                    reply envelope BloggerRepairOutcome.PendingRepairWait
                    return! awaitAabb nudgeRun nextPort
                | Some permit -> return! handleAabbConsume nudgeRun run ports permit envelope nextPort
            }

        and handleAabbConsume
            nudgeRun
            run
            ports
            permit
            envelope
            nextPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                match ports.Quiescence.TryConsume permit with
                | Error _ ->
                    reply envelope BloggerRepairOutcome.PendingRepairWait
                    return! awaitAabb nudgeRun nextPort
                | Ok() -> return! sendAabbAfterConsume run ports envelope nextPort
            }

        and sendAabbAfterConsume
            run
            ports
            envelope
            nextPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                let! physical = tryAabbPhysical ports run

                match physical with
                | Error msg ->
                    do! abandonAndResolve envelope nextPort ("blogger AABB repair faulted: " + msg)
                    return None
                | Ok outcome -> return! decideAabbOutcome outcome run envelope nextPort
            }

        and decideAabbOutcome
            outcome
            run
            envelope
            nextPort
            : Task<(ProviderRunIdentity * IEventObservationPort option) option> =
            task {
                match outcome with
                | InteractionRepairSendOutcome.Sent promptKey ->
                    reply envelope (BloggerRepairOutcome.AabbSent(Some promptKey))
                    return Some(run, nextPort)
                | InteractionRepairSendOutcome.AlreadyAdmitted ->
                    reply envelope (BloggerRepairOutcome.AabbSent None)
                    return Some(run, nextPort)
                | InteractionRepairSendOutcome.Retired ->
                    do! abandonAndResolve envelope nextPort "blogger protocol repair exhausted"
                    return None
                | InteractionRepairSendOutcome.Failed error ->
                    do! abandonAndResolve envelope nextPort ("blogger protocol repair failed: " + error)
                    return None
            }

        let rec awaitExhaust nudgeRun aabbRun eventPort : Task =
            task {
                let! envelopeOpt = rendezvous.Receive()

                match envelopeOpt with
                | None -> return ()
                | Some envelope -> return! settleExhaustEnvelope nudgeRun aabbRun envelope eventPort
            }

        and settleExhaustEnvelope nudgeRun aabbRun envelope eventPort : Task =
            task {
                try
                    return! handleExhaustEnvelope nudgeRun aabbRun envelope eventPort
                with ex ->
                    do! abandonAndResolve envelope eventPort ("blogger repair exhaustion faulted: " + ex.Message)
                    return ()
            }

        and handleExhaustEnvelope nudgeRun aabbRun envelope eventPort : Task =
            task {
                match envelope.Observation with
                | BloggerRepairObservation.TransformToolFacts(run, _) ->
                    return! handleExhaustTransform nudgeRun aabbRun run envelope eventPort
                | BloggerRepairObservation.IdleQuiescentTurn(run, ports) ->
                    return! settleExhaustIdle nudgeRun aabbRun run ports envelope eventPort
            }

        and handleExhaustTransform nudgeRun aabbRun run envelope eventPort : Task =
            task {
                match run = nudgeRun || run = aabbRun with
                | true ->
                    reply envelope BloggerRepairOutcome.PendingRepairWait
                    return! awaitExhaust nudgeRun aabbRun eventPort
                | false ->
                    do! abandonAndResolve envelope eventPort "blog aabb exhausted; auto-recovery budget spent"
                    return ()
            }

        and settleExhaustIdle nudgeRun aabbRun run ports envelope eventPort : Task =
            task {
                let nextPort = Some ports.EventPort

                try
                    return! handleExhaustIdleDuplicate nudgeRun aabbRun run ports envelope nextPort
                with ex ->
                    do! abandonAndResolve envelope nextPort ("blogger repair exhaustion faulted: " + ex.Message)
                    return ()
            }

        and handleExhaustIdleDuplicate nudgeRun aabbRun run ports envelope nextPort : Task =
            task {
                match run = nudgeRun || run = aabbRun with
                | true ->
                    reply envelope BloggerRepairOutcome.PendingRepairWait
                    return! awaitExhaust nudgeRun aabbRun nextPort
                | false -> return! handleExhaustQuiescence nudgeRun aabbRun ports envelope nextPort
            }

        and handleExhaustQuiescence nudgeRun aabbRun ports envelope nextPort : Task =
            task {
                match ports.Context.Quiescence with
                | None ->
                    reply envelope BloggerRepairOutcome.PendingRepairWait
                    return! awaitExhaust nudgeRun aabbRun nextPort
                | Some _ ->
                    do! abandonAndResolve envelope nextPort "blogger protocol repair exhausted"
                    return ()
            }

        let runAfterNudge nudgeRun portAfterNudge : Task =
            task {
                let! aabbed = awaitAabb nudgeRun portAfterNudge

                match aabbed with
                | None -> return ()
                | Some(aabbRun, portAfterAabb) -> do! awaitExhaust nudgeRun aabbRun portAfterAabb
            }

        task {
            let! nudged = awaitNudge None

            match nudged with
            | None -> ()
            | Some(nudgeRun, portAfterNudge) -> do! runAfterNudge nudgeRun portAfterNudge

            rendezvous.Cancel()
        }

    /// Transform repair entry: posts exact observed terminal/tool facts to the
    /// request-scoped owner episode. Main/blogger ids come from the live request;
    /// authority comes from the current durable Blogger profile. The exact live
    /// flight and terminal ownership are verified before Claim/Post, so stale or
    /// unowned observations cause no effect.
    let private claimAndPostTransform
        (scope: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (identity: BloggerRepairEpisodeIdentity)
        (request: BloggerRequestContext)
        (terminalRun: ProviderRunIdentity)
        (rawMessages: obj list)
        : Task<BloggerRepairOutcome> =
        task {
            match scope.ClaimRepairEpisode identity with
            | Error _ -> return BloggerRepairOutcome.SupersededIgnored
            | Ok rendezvous ->
                rendezvous.Start(fun () -> runRepairEpisode scope durable identity request rendezvous)
                |> ignore

                let! outcome = rendezvous.Post(BloggerRepairObservation.TransformToolFacts(terminalRun, rawMessages))
                return outcome
        }

    let private checkTransformOwnership
        (scope: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (request: BloggerRequestContext)
        (identity: BloggerRepairEpisodeIdentity)
        (terminalRun: ProviderRunIdentity)
        (rawMessages: obj list)
        : Task<BloggerRepairOutcome> =
        task {
            let bloggerId = BloggerRequestContext.bloggerSessionId request

            match
                BloggerRecoveryProbe.terminalRequestOwnershipForProviderRun
                    Wanxiangshu.OpenCode.ProviderWireCapture.tryPhysicalParentOfProviderRun
                    durable
                    bloggerId
                    request
                    terminalRun
                    rawMessages
            with
            | BloggerTerminalRequestOwnership.Superseded -> return BloggerRepairOutcome.SupersededIgnored
            | BloggerTerminalRequestOwnership.Current
            | BloggerTerminalRequestOwnership.Unproven ->
                return! claimAndPostTransform scope durable identity request terminalRun rawMessages
        }

    let private checkTransformFlight
        (scope: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (request: BloggerRequestContext)
        (identity: BloggerRepairEpisodeIdentity)
        (terminalRun: ProviderRunIdentity)
        (rawMessages: obj list)
        : Task<BloggerRepairOutcome> =
        task {
            match exactFlightMatches scope request with
            | true -> return! checkTransformOwnership scope durable request identity terminalRun rawMessages
            | false -> return BloggerRepairOutcome.SupersededIgnored
        }

    let private continueTransformWithJournal
        (scope: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (request: BloggerRequestContext)
        (terminalRun: ProviderRunIdentity)
        (rawMessages: obj list)
        : Task<BloggerRepairOutcome> =
        task {
            match activeAuthorityRoot durable (BloggerRequestContext.bloggerSessionId request) with
            | None -> return BloggerRepairOutcome.SupersededIgnored
            | Some authorityRoot ->
                let identity: BloggerRepairEpisodeIdentity =
                    { RequestId = BloggerRequestContext.requestId request
                      AuthorityRoot = authorityRoot
                      MainSessionId = BloggerRequestContext.mainSessionId request
                      BloggerSessionId = BloggerRequestContext.bloggerSessionId request }

                return! checkTransformFlight scope durable request identity terminalRun rawMessages
        }

    let observeTransformRepair
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal option)
        (request: BloggerRequestContext)
        (terminalRun: ProviderRunIdentity)
        (rawMessages: obj list)
        : Task<BloggerRepairOutcome> =
        task {
            match journal with
            | None -> return BloggerRepairOutcome.AbandonedExhausted
            | Some durable -> return! continueTransformWithJournal scope durable request terminalRun rawMessages
        }

    /// Idle repair entry: posts the exact quiescence + terminal observation to the
    /// same request-scoped owner episode. Verification mirrors the transform entry;
    /// a missing live flight yields UnownedIdleIgnored with no budget spent. The
    /// interface root workspace crosses into the episode as its typed TryRead port.
    let private claimAndPostIdle
        (scope: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (identity: BloggerRepairEpisodeIdentity)
        (request: BloggerRequestContext)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        : Task<BloggerRepairOutcome> =
        task {
            match scope.ClaimRepairEpisode identity with
            | Error _ -> return BloggerRepairOutcome.SupersededIgnored
            | Ok rendezvous ->
                rendezvous.Start(fun () -> runRepairEpisode scope durable identity request rendezvous)
                |> ignore

                let ports: BloggerRepairTerminalPorts =
                    { Quiescence = quiescence
                      Context = context
                      SessionPort = sessionPort
                      TryReadRootWorkspace = rootWorkspace.TryRead
                      EventPort = eventPort }

                let! outcome =
                    rendezvous.Post(BloggerRepairObservation.IdleQuiescentTurn(context.Turn.ProviderRun, ports))

                return outcome
        }

    let private checkIdleOwnership
        (scope: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (request: BloggerRequestContext)
        (identity: BloggerRepairEpisodeIdentity)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        : Task<BloggerRepairOutcome> =
        task {
            match
                BloggerRecoveryProbe.terminalRequestOwnershipForPhysicalMessage
                    durable
                    (BloggerRequestContext.bloggerSessionId request)
                    request
                    context.Turn.PhysicalUserMessageId
            with
            | BloggerTerminalRequestOwnership.Superseded -> return BloggerRepairOutcome.SupersededIgnored
            | BloggerTerminalRequestOwnership.Current
            | BloggerTerminalRequestOwnership.Unproven ->
                return!
                    claimAndPostIdle
                        scope
                        durable
                        identity
                        request
                        quiescence
                        context
                        sessionPort
                        rootWorkspace
                        eventPort
        }

    let private checkIdleFlight
        (scope: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (request: BloggerRequestContext)
        (identity: BloggerRepairEpisodeIdentity)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        : Task<BloggerRepairOutcome> =
        task {
            match exactFlightMatches scope request with
            | true ->
                return!
                    checkIdleOwnership
                        scope
                        durable
                        request
                        identity
                        quiescence
                        context
                        sessionPort
                        rootWorkspace
                        eventPort
            | false -> return BloggerRepairOutcome.UnownedIdleIgnored
        }

    let private continueIdleWithJournal
        (scope: IBloggerRuntimeHost)
        (durable: AgentJournal)
        (request: BloggerRequestContext)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        : Task<BloggerRepairOutcome> =
        task {
            match activeAuthorityRoot durable (BloggerRequestContext.bloggerSessionId request) with
            | None -> return BloggerRepairOutcome.SupersededIgnored
            | Some authorityRoot ->
                let identity: BloggerRepairEpisodeIdentity =
                    { RequestId = BloggerRequestContext.requestId request
                      AuthorityRoot = authorityRoot
                      MainSessionId = BloggerRequestContext.mainSessionId request
                      BloggerSessionId = BloggerRequestContext.bloggerSessionId request }

                return!
                    checkIdleFlight
                        scope
                        durable
                        request
                        identity
                        quiescence
                        context
                        sessionPort
                        rootWorkspace
                        eventPort
        }

    let observeIdleRepair
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal option)
        (request: BloggerRequestContext)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        : Task<BloggerRepairOutcome> =
        task {
            match journal with
            | None -> return BloggerRepairOutcome.AbandonedExhausted
            | Some durable ->
                return!
                    continueIdleWithJournal scope durable request quiescence context sessionPort rootWorkspace eventPort
        }

    [<RequireQualifiedAccess>]
    type DecisionEffect =
        | Started
        | StartedSquash
        | SkippedInFlight
        | OfferedParked
        | NoMaterial
        | Sealed
        | StartFailed of string
        | MaterializeFailed of string

    let private encodeContextPayload (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main ->
            "main",
            main.PreviousIngestedThroughSequence,
            main.NextIngestedThroughSequence,
            [],
            createObj
                [ "kind", box "main"
                  "items", box (main.Items |> List.map BloggerDeltaItemWire.toJs |> List.toArray)
                  "toml", box main.Toml
                  "delta_digest", box (BlobDigest.value main.DeltaDigest)
                  "prev_ingest", box main.PreviousIngestedThroughSequence
                  "next_ingest", box main.NextIngestedThroughSequence
                  "prev_cutoff", box main.PreviousCoverableTurnCutoffExclusive
                  "next_cutoff", box main.NextCoverableTurnCutoffExclusive
                  "next_prefix_digest", box main.NextCoveredPrefixDigest
                  "frame_epoch", box (FrameEpochId.value main.FrameEpochId)
                  "observed_prefix_epoch", box (PrefixEpochId.value main.ObservedPrefixEpochId) ]
        | BloggerRequestContext.Squash squash ->
            "squash",
            0L,
            0L,
            squash.FrameDigests,
            createObj
                [ "kind", box "squash"
                  "frame_epoch", box (FrameEpochId.value squash.FrameEpochId)
                  "covered_frame_count", box squash.CoveredFrameCount
                  "frame_digests", box (squash.FrameDigests |> List.map BlobDigest.value |> Array.ofList)
                  "observed_prefix_epoch", box (PrefixEpochId.value squash.ObservedPrefixEpochId) ]

    let private abandonStaleOpen
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        (staleOpen: OpenBloggerRequest option)
        : Task<unit> =
        task {
            match staleOpen with
            | Some openReq when openReq.RequestId <> requestId ->
                do!
                    BloggerAbandon.byRequestId
                        journal
                        openReq.RequestId
                        mainSessionId
                        bloggerSessionId
                        "superseded-by-new-materialize"
            | _ -> ()
        }

    let private resolveContextBlob
        (journal: AgentJournal)
        (existingOpen: OpenBloggerRequest option)
        (promptKey: PromptKey option)
        (contextPayload: obj)
        : Task<Result<BlobRef * BlobDigest, string>> =
        match existingOpen with
        | Some openReq when openReq.PromptKey.IsNone && promptKey.IsSome ->
            Task.FromResult(Ok(openReq.ContextRef, openReq.ContextDigest))
        | Some openReq when openReq.PromptKey = promptKey ->
            Task.FromResult(Ok(openReq.ContextRef, openReq.ContextDigest))
        | _ ->
            taskResult {
                let! blob = journal.WriteBlob(Wanxiangshu.Foundation.CanonicalJson.canonicalJson contextPayload)
                return blob.BlobRef, blob.BlobDigest
            }

    let private appendMaterialized
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (fact: AgentFact)
        : Task<Result<unit, string>> =
        task {
            let! result = AgentJournal.appendAgent (StreamId.Session mainSessionId) None fact journal
            return result |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
        }

    /// C5: durable materialization. Context blob is the irrecomputable semantic
    /// input. Pre-send PromptKey=None; after physical send, re-append with the
    /// same ContextDigest + Some PromptKey so commit can prove ownership.
    let private materializeRequest
        (journal: AgentJournal)
        (ctx: BloggerRequestContext)
        (promptKey: PromptKey option)
        : Task<Result<unit, string>> =
        taskResult {
            let requestId = BloggerRequestContext.requestId ctx
            let mainSessionId = BloggerRequestContext.mainSessionId ctx
            let bloggerSessionId = BloggerRequestContext.bloggerSessionId ctx
            let epoch = BloggerRequestContext.observedPrefixEpoch ctx
            let frameEpoch = BloggerRequestContext.frameEpochId ctx

            let kind, prevSeq, nextSeq, selectedDigests, contextPayload =
                encodeContextPayload ctx

            // One open request per Blogger. Restart / re-offer with a new RequestId
            // must supersede a stale open slot (fold rejects two opens on one session).
            // PromptKey fill-in reuses the existing open context blob/digest.
            let projections = AgentJournal.snapshot journal

            let existingOpen =
                projections.AgentProjections.Sessions
                |> Map.tryFind mainSessionId
                |> Option.bind (fun s -> s.BloggerCycles)
                |> Option.bind (fun cycles -> Map.tryFind requestId cycles.OpenByRequestId)

            let staleOpen =
                projections.AgentProjections.Sessions
                |> Map.tryFind mainSessionId
                |> Option.bind (fun s -> s.BloggerCycles)
                |> Option.bind (fun cycles -> BloggerCycleProjection.tryOpenByBlogger bloggerSessionId cycles)

            do!
                abandonStaleOpen journal mainSessionId bloggerSessionId requestId staleOpen
                |> TaskResultCE.ofTask

            let! contextRef, contextDigest = resolveContextBlob journal existingOpen promptKey contextPayload

            let fact =
                ContextFact.BloggerRequestMaterialized
                    {| RequestId = requestId
                       MainSessionId = mainSessionId
                       BloggerSessionId = bloggerSessionId
                       RequestKind = kind
                       ContextRef = contextRef
                       ContextDigest = contextDigest
                       ObservedPrefixEpochId = epoch
                       PreviousIngestedThroughSequence = prevSeq
                       NextIngestedThroughSequence = nextSeq
                       FrameEpochId = frameEpoch
                       SelectedFrameDigests = selectedDigests
                       PromptKey = promptKey |}

            do! appendMaterialized journal mainSessionId fact
        }

    let private abandonRequest (journal: AgentJournal option) (ctx: BloggerRequestContext) (reason: string) : Task =
        match journal with
        | None -> Task.FromResult(()) :> Task
        | Some j ->
            BloggerAbandon.openRequest
                j
                (BloggerRequestContext.mainSessionId ctx)
                (BloggerRequestContext.bloggerSessionId ctx)
                (Some ctx)
                reason

    let private withMaterialization
        (scope: IBloggerRuntimeHost)
        (bloggerSessionId: SessionId)
        (work: unit -> Task<'T>)
        : Task<'T> =
        task {
            let! lease = scope.AcquireMaterialization(SessionId.value bloggerSessionId)

            try
                return! work ()
            finally
                lease.Release()
        }

    let private foreignFlightReason (scope: IBloggerRuntimeHost) (ctx: BloggerRequestContext) : string option =
        let bloggerKey = SessionId.value (BloggerRequestContext.bloggerSessionId ctx)
        let requestId = BloggerRequestContext.requestId ctx

        match scope.TryGetFlight bloggerKey with
        | Some existing when BloggerRequestContext.requestId existing <> requestId ->
            Some(
                sprintf
                    "Blogger flight %s already belongs to request %s; refusing to materialize request %s"
                    bloggerKey
                    (BloggerRequestId.value (BloggerRequestContext.requestId existing))
                    (BloggerRequestId.value requestId)
            )
        | _ -> None

    let private claimMaterializedContinuation
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal)
        (ctx: BloggerRequestContext)
        : Task<Result<unit, string>> =
        task {
            match
                BloggerRuntimeHost.claimCurrentRequest
                    scope
                    (SessionId.value (BloggerRequestContext.bloggerSessionId ctx))
                    ctx
            with
            | Ok() -> return Ok()
            | Error reason ->
                do! abandonRequest (Some journal) ctx ("materialized flight claim failed: " + reason)
                return Error reason
        }

    let private materializeAndClaimContinuation
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal)
        (ctx: BloggerRequestContext)
        : Task<Result<unit, string>> =
        taskResult {
            do! materializeRequest journal ctx None
            return! claimMaterializedContinuation scope journal ctx
        }

    let materializeContinuationContext
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal)
        (ctx: BloggerRequestContext)
        : Task<Result<unit, string>> =
        withMaterialization scope (BloggerRequestContext.bloggerSessionId ctx) (fun () ->
            match foreignFlightReason scope ctx with
            | Some reason -> Task.FromResult(Error reason)
            | None -> materializeAndClaimContinuation scope journal ctx)

    let bindContinuationContext
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal)
        (ctx: BloggerRequestContext)
        (promptKey: PromptKey)
        =
        withMaterialization scope (BloggerRequestContext.bloggerSessionId ctx) (fun () ->
            task {
                match
                    BloggerRuntimeHost.claimCurrentRequest
                        scope
                        (SessionId.value (BloggerRequestContext.bloggerSessionId ctx))
                        ctx
                with
                | Error reason -> return Error reason
                | Ok() -> return! materializeRequest journal ctx (Some promptKey)
            })

    let abandonContinuationContext
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal)
        (ctx: BloggerRequestContext)
        (reason: string)
        : Task =
        withMaterialization scope (BloggerRequestContext.bloggerSessionId ctx) (fun () ->
            task {
                do! abandonRequest (Some journal) ctx reason

                BloggerRuntimeHost.releaseCurrentRequest
                    scope
                    (SessionId.value (BloggerRequestContext.bloggerSessionId ctx))
                    ctx
                |> ignore
            })

    let private startedEffect (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Squash _ -> DecisionEffect.StartedSquash
        | BloggerRequestContext.Main _ -> DecisionEffect.Started

    let private failAfterSend
        (journal: AgentJournal option)
        (host: CompanionHost)
        (scope: IBloggerRuntimeHost)
        (key: string)
        (ctx: BloggerRequestContext)
        (reason: string)
        : Task<DecisionEffect> =
        task {
            do! abandonRequest journal ctx reason
            BloggerRuntimeHost.releaseCurrentRequest scope key ctx |> ignore
            host.InvalidateBloggerCache()
            return DecisionEffect.StartFailed reason
        }

    let private failBeforeFlightClaim
        (journal: AgentJournal option)
        (host: CompanionHost)
        (ctx: BloggerRequestContext)
        (reason: string)
        : Task<DecisionEffect> =
        task {
            do! abandonRequest journal ctx ("materialized flight claim failed: " + reason)
            host.InvalidateBloggerCache()
            return DecisionEffect.StartFailed reason
        }

    let private bindSendAndPostMaterialize
        (host: CompanionHost)
        (j: AgentJournal)
        (ctx: BloggerRequestContext)
        : Task<Result<DecisionEffect, string>> =
        taskResult {
            let! promptKey = host.StartFromContext(ctx)
            do! materializeRequest j ctx (Some promptKey)
            return startedEffect ctx
        }

    let private finishClaimedSend
        (journal: AgentJournal option)
        (host: CompanionHost)
        (scope: IBloggerRuntimeHost)
        (j: AgentJournal)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        task {
            let! outcome = bindSendAndPostMaterialize host j ctx

            match outcome with
            | Ok effect -> return effect
            | Error reason -> return! failAfterSend journal host scope key ctx reason
        }

    let private proceedAfterPreSend
        (scope: IBloggerRuntimeHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (j: AgentJournal)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        task {
            match BloggerRuntimeHost.claimCurrentRequest scope key ctx with
            | Error reason -> return! failBeforeFlightClaim journal host ctx reason
            | Ok() -> return! finishClaimedSend journal host scope j key ctx
        }

    let private afterPreSendMaterialize
        (scope: IBloggerRuntimeHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (j: AgentJournal)
        (mainId: SessionId)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        if BloggerRuntimeHost.blocksNew (Some j) mainId then
            task {
                do! abandonRequest journal ctx "main-sealed-before-send"
                scope.CancelParked key
                return DecisionEffect.Sealed
            }
        else
            proceedAfterPreSend scope host journal j key ctx

    let private materializeThenSend
        (scope: IBloggerRuntimeHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (j: AgentJournal)
        (mainId: SessionId)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        task {
            match! materializeRequest j ctx None with
            | Error reason -> return DecisionEffect.MaterializeFailed reason
            | Ok() -> return! afterPreSendMaterialize scope host journal j mainId key ctx
        }

    let private startWithJournal
        (scope: IBloggerRuntimeHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (j: AgentJournal)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        // Order (C2/C5): admission → materialize durable → atomic flight claim → send.
        // The admission is process-local serialization only; the durable open request
        // remains the producer/ownership proof.
        let mainId = BloggerRequestContext.mainSessionId ctx

        withMaterialization scope (BloggerRequestContext.bloggerSessionId ctx) (fun () ->
            if BloggerRuntimeHost.blocksNew (Some j) mainId then
                scope.CancelParked key
                Task.FromResult DecisionEffect.Sealed
            elif scope.TryGetFlight key |> Option.isSome then
                Task.FromResult DecisionEffect.SkippedInFlight
            elif BloggerRuntimeHost.hasOpenProducer (Some j) mainId (BloggerRequestContext.bloggerSessionId ctx) then
                scope.OfferMaterial(key, ctx) |> ignore
                Task.FromResult DecisionEffect.OfferedParked
            elif scope.TryDeliverMaterial(key, ctx) then
                Task.FromResult DecisionEffect.OfferedParked
            else
                materializeThenSend scope host journal j mainId key ctx)

    let private startFrozen
        (scope: IBloggerRuntimeHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        match journal with
        | None -> Task.FromResult(DecisionEffect.MaterializeFailed "no journal")
        | Some j -> startWithJournal scope host journal j key ctx

    let private applyMainDecision
        (scope: IBloggerRuntimeHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        if
            BloggerRuntimeHost.hasOpenProducer
                journal
                (BloggerRequestContext.mainSessionId ctx)
                (BloggerRequestContext.bloggerSessionId ctx)
        then
            scope.OfferMaterial(key, ctx) |> ignore
            Task.FromResult DecisionEffect.OfferedParked
        elif scope.TryDeliverMaterial(key, ctx) then
            Task.FromResult DecisionEffect.OfferedParked
        else
            startFrozen scope host journal key ctx

    /// Unique production entry for an already-derived main Blogger context.
    /// Context derivation belongs to BloggerMainContext; this coordinator owns
    /// only physical materialization / offer / flight effects.
    let onMainContext
        (scope: IBloggerRuntimeHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        let mainSessionId = BloggerRequestContext.mainSessionId ctx
        let bloggerSessionId = BloggerRequestContext.bloggerSessionId ctx
        let key = SessionId.value bloggerSessionId

        if BloggerRuntimeHost.blocksNew journal mainSessionId then
            scope.CancelParked key
            Task.FromResult DecisionEffect.Sealed
        elif scope.TryGetFlight key |> Option.isSome then
            // Busy = physical flight ownership, not cell.State match.
            Task.FromResult DecisionEffect.SkippedInFlight
        else
            applyMainDecision scope host journal key ctx
