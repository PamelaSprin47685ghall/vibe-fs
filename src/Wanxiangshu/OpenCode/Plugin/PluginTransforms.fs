namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
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
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Mission.Relay.OpenCode
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Enforcer
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Resources
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
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open PluginHostInterop

module PluginTransforms =

    type private SessionTermination = SessionId -> string -> Task<Result<unit, string>>

    type TraceTransformCapture =
        { RawMessages: obj list
          Current: XTraceProjectionState option }

    type NormalTransformCapabilities =
        { BeginPhysicalProviderAttempt: string option -> obj -> Task<unit>
          BindSessionStartedAt: string option -> Task<DateTimeOffset option>
          ApplyStrengthReplay: string option -> obj -> Task<StrengthReplayPlan list>
          ApplyRelayProjection: string option -> obj -> Task<unit>
          CaptureXTraceMessages: string option -> obj -> Task<TraceTransformCapture>
          CommitStrengthTrace: string option -> XTraceProjectionState option -> StrengthReplayPlan list -> Task<unit>
          RefreshCompanionXTrace: string option -> XTraceProjectionState option -> unit
          ApplyCompanion: string option -> obj -> obj -> Task<unit>
          ApplyXWire: obj -> Task<PrefixPresentationHorizon>
          FreezeProviderAttemptPlan: string option -> obj -> Task<unit>
          ApplyEnforcerContinuation: string option -> obj -> Task<unit>
          ApplyStrengthSpeculate: obj -> Task<unit>
          InjectPairGuideline: string option -> DateTimeOffset option -> obj -> Task<unit>
          ProjectRequirementGrounding: string option -> obj -> Task<unit>
          InjectBloggerChronicle: string option -> obj -> unit
          SanitizeMessages: obj -> unit }

    type TransformBranchCapabilities =
        { IsExplicitResume: string option -> obj -> bool
          RegisterOwned: string -> unit
          ReplicaRuntime: string option -> StrengthReplicaRuntime option
          ReplicaXWire: obj -> Task<unit>
          ReplicaSanitize: obj -> unit
          ExplicitResumeSanitize: obj -> unit }


    let private languageFor (projectionSessionIdOpt: string option) : ProviderLanguage =
        match projectionSessionIdOpt with
        | Some sessionId -> ProviderLanguageBinding.ensureRoot (SessionId.create sessionId)
        | None -> ProviderLanguageBinding.readGlobalPreference ()

    // Explicit composition mode — replaces the previous implicit helper dispatch
    // (strengthReplicaRuntime / isExplicitResumeProviderMaterial / ordinaryProviderTransform).
    // This type is representation-level (composition topology), not a foreign domain decision.
    type private TransformMode =
        | ExplicitResumeDisclosure
        | StrengthReplica of StrengthReplicaRuntime
        | Ordinary

    let private failIfReplicaDecisionLost (handled: bool) : unit =
        if not handled then
            raise (InvalidOperationException "StrengthReplica transform lost its live decision binding")

    let private raiseFailClosed (fuse: string -> unit) (reason: string) : 'a =
        fuse reason
        raise (InvalidOperationException reason)

    let private decodePromptOrigin (label: string) : PromptAuthority.PromptOrigin =
        match label with
        | "HumanRoot" -> PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot
        | "AgentOwnerRoot" ->
            PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        | "HostInternal" -> PromptAuthority.PromptOrigin.HostInternal
        | "UnknownOrigin" -> PromptAuthority.PromptOrigin.UnknownOrigin
        | continuation ->
            continuation
            |> PromptAuthority.tryParseContinuationKind
            |> Option.map PromptAuthority.PromptOrigin.Continuation
            |> Option.defaultValue PromptAuthority.PromptOrigin.UnknownOrigin

    let private determineTransformMode
        (branches: TransformBranchCapabilities)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        : TransformMode =
        match
            branches.IsExplicitResume projectionSessionIdOpt outObj, branches.ReplicaRuntime projectionSessionIdOpt
        with
        | true, _ -> ExplicitResumeDisclosure
        | false, Some runtime -> StrengthReplica runtime
        | false, None -> Ordinary

    let defaultCapabilities (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : NormalTransformCapabilities =
        let scope = boot.Scope
        let journal = boot.Journal
        let clock = boot.Clock
        let workspaceDirectory = boot.WorkspaceDirectory
        let sessionPort = host.SessionPort
        let eventPort = host.EventPort
        let snapshotOpt = host.SnapshotOpt
        let strengthDurability = host.StrengthDurability
        let wired = host.Wired
        let strengthFailFuse = boot.StrengthFailClosed

        let drainTermination sessionId =
            function
            | Error error -> Task.FromResult(Error error)
            | Ok() ->
                task {
                    do! scope.DrainChatRecovery sessionId
                    return Ok()
                }

        let terminatePhysical sessionId reason physical =
            task {
                do! scope.SignalChatRecoverySession sessionId ChatExecutionRecoveryLifecycleEvent.SessionCancelled

                let! termination =
                    ManagedSessionTermination.terminate
                        (fun ownerId -> scope.CancelSessionChildren(SessionId.value ownerId))
                        sessionPort
                        eventPort
                        sessionId
                        (physical
                         |> PhysicalUserMessageId.create
                         |> PhysicalUserMessageId.promoteToAuthorityRoot)
                        reason

                return! drainTermination sessionId termination
            }

        let terminateSession: SessionTermination =
            fun sessionId reason ->
                wired.CurrentPhysicalUserMessage(SessionId.value sessionId)
                |> Option.map (terminatePhysical sessionId reason)
                |> Option.defaultWith (fun () ->
                    Task.FromResult(Error "MANAGED-SESSION-017: current authority root unavailable"))

        let freezeProviderAttemptPlan projectionSessionIdOpt outObj =
            task {
                match!
                    SessionExecutionBinding.freezeProviderAttemptPlanForTransform
                        journal
                        scope.Recovery.FreezePendingAttemptPlan
                        projectionSessionIdOpt
                        outObj
                with
                | Ok _ -> return ()
                | Error error ->
                    return
                        invalidOp (
                            sprintf
                                "HOST-BOUNDARY-008: provider attempt plan freeze failed (%s): %A"
                                (SessionExecutionBinding.providerStartObservationErrorCode error)
                                error
                        )
            }

        let tryCaptureSnapshot dirOpt =
            dirOpt
            |> Option.filter (String.IsNullOrWhiteSpace >> not)
            |> Option.bind (fun dir ->
                try Some (WorkspaceSnapshot.capture dir) with _ -> None)
            |> Option.defaultValue (WorkspaceSnapshotId.create "snapshot-root")

        let buildOpeningEvents roadId sessionIdText rootUserMsg dirOpt =
            let authorityRevision = AuthorityRevision.create rootUserMsg
            let authorityMessageId = PhysicalUserMessageId.create rootUserMsg
            let incumbent =
                HostDigest.sha256Hex ("incumbency-v1\n" + sessionIdText + "\n" + rootUserMsg)
                |> fun digest -> IncumbencyId.create ("incumbency:" + digest)
            let snapshotId = tryCaptureSnapshot dirOpt
            [ RelayEvent.RoadOpened(roadId, authorityRevision, authorityMessageId)
              RelayEvent.IncumbencyOpened(incumbent, snapshotId, BatonSource.ExistingWorld) ]

        let commitOpeningTransaction (durable: AgentJournal) sessionId providerRunIdOpt roadId events =
            task {
                match RelayTransaction.create events with
                | Error _ -> return ()
                | Ok tx ->
                    let fact =
                        AgentFact.Relay(
                            RelayFactCases.TransactionCommitted
                                {| RoadId = roadId
                                   Transaction = tx |}
                        )
                    let! _ = AgentJournal.appendAgent (StreamId.Session sessionId) providerRunIdOpt fact durable
                    return ()
            }

        let decideOpeningAction sessionIdTextOpt =
            sessionIdTextOpt
            |> Option.filter (String.IsNullOrWhiteSpace >> not)
            |> Option.bind (fun sessionIdText ->
                journal
                |> Option.bind (fun durable ->
                    let sessionId = SessionId.create sessionIdText
                    let snapshot = AgentJournal.snapshot durable
                    let roadId = RoadId.create sessionIdText

                    let roadView =
                        AgentProjection.tryFind sessionId snapshot.AgentProjections
                        |> Option.bind (fun (s: SessionAgentProjection) -> s.Relay)
                        |> Option.bind (fun (r: RelayState) -> Fold.view r roadId)

                    let profileOpt = PromptAuthorityLedger.activeProfile sessionId snapshot.AgentProjections
                    let isManager = profileOpt |> Option.exists (fun p -> p.CanonicalRole = Role.Manager)

                    if roadView.IsNone && isManager then
                        let rootUserMsg =
                            profileOpt
                            |> Option.map (fun p -> AuthorityRootUserMessageId.value p.AuthorityRootUserMessageId)
                            |> Option.defaultValue sessionIdText

                        let events = buildOpeningEvents roadId sessionIdText rootUserMsg workspaceDirectory
                        Some(durable, sessionId, roadId, events)
                    else
                        None))

        let ensureManagerRoadOpened
            (sessionIdTextOpt: string option)
            (providerRunIdOpt: ProviderRunIdentity option)
            : Task<unit> =
            task {
                match decideOpeningAction sessionIdTextOpt with
                | None -> return ()
                | Some(durable, sessionId, roadId, events) ->
                    do! commitOpeningTransaction durable sessionId providerRunIdOpt roadId events
            }

        let buildSuccessorEvents roadId retirementId snapshot authorityRevision =
            let successorIncumbent =
                HostDigest.sha256Hex ("successor-v1\n" + RetirementId.value retirementId)
                |> fun digest -> IncumbencyId.create ("incumbency:" + digest)
            [ RelayEvent.SuccessorRequested(retirementId, "IndependentAssessmentRequired")
              RelayEvent.SuccessorActivated(retirementId, successorIncumbent, snapshot, authorityRevision) ]

        let deliverSuccessorPrompt (durable: AgentJournal) sessionId retirement =
            task {
                let successorPromptText =
                    ProviderProse.documentFor sessionId "runtime/relay-successor" Map.empty
                let terminalRun =
                    ProviderRunIdentity.create retirement.ProjectionCut.ThroughProviderRunId

                let! _ =
                    HostSessionNudge.trySendGateContinuationPhysical
                        sessionPort
                        host.RootWorkspace
                        sessionId
                        successorPromptText
                        PromptAuthority.ContinuationKind.ManagerGuard
                        workspaceDirectory
                        (Some durable)
                        (RelaySuccessorGate.gateKind retirement.Id)
                        terminalRun
                return ()
            }

        let maybeDeliverSuccessor sessionIdTextOpt =
            task {
                let contextOpt =
                    match sessionIdTextOpt, journal with
                    | Some sidText, Some durable when not (String.IsNullOrWhiteSpace sidText) ->
                        let sessionId = SessionId.create sidText
                        let roadId = RoadId.create sidText
                        let snapshot = AgentJournal.snapshot durable
                        AgentProjection.tryFind sessionId snapshot.AgentProjections
                        |> Option.bind (fun (s: SessionAgentProjection) -> s.Relay)
                        |> Option.bind (fun (r: RelayState) -> Fold.view r roadId)
                        |> Option.bind (fun road ->
                            road.LatestRetirement
                            |> Option.filter (fun ret -> ret.SuccessorRequested && road.ActiveIncumbency.IsNone)
                            |> Option.map (fun ret -> durable, sessionId, roadId, road.AuthorityRevision, ret))
                    | _ -> None

                match contextOpt with
                | Some(durable, sessionId, roadId, authorityRevision, retirement) ->
                    let snapshot = tryCaptureSnapshot workspaceDirectory
                    let events = buildSuccessorEvents roadId retirement.Id snapshot authorityRevision
                    do! commitOpeningTransaction durable sessionId None roadId events
                    do! deliverSuccessorPrompt durable sessionId retirement
                | None -> return ()
            }

        { BeginPhysicalProviderAttempt =
            SessionExecutionBinding.beginPhysicalProviderAttemptForTransform
                scope.Sessions.Quiescence.BeginProviderAttempt
          BindSessionStartedAt =
            SessionStartedAtLedger.bindSessionStartedAt journal clock terminateSession Diagnostic.emit
          ApplyStrengthReplay = StrengthReplay.applyBeforeXTrace journal strengthDurability strengthFailFuse
          ApplyRelayProjection =
            fun sidOpt outObj ->
                task {
                    do! ensureManagerRoadOpened sidOpt None
                    do! maybeDeliverSuccessor sidOpt

                    do!
                        RelayNarrativeTransform.apply journal (fun sid ->
                            task {
                                let! _ = sessionPort.InterruptAttempt sid
                                return ()
                            }) sidOpt outObj
                }
          CaptureXTraceMessages =
            fun projectionSessionIdOpt outObj ->
                task {
                    let rawMessages = unbox<obj array> outObj?messages |> Array.toList

                    match projectionSessionIdOpt with
                    | None ->
                        return
                            { RawMessages = rawMessages
                              Current = None }
                    | Some sessionId ->
                        let observations =
                            rawMessages
                            |> List.choose (fun rawMessage ->
                                ProviderWireCapture.decodeCapturedMessage rawMessage
                                |> Option.map (fun message ->
                                    { Message = message
                                      HostMessageId = ProviderWireDecode.hostMessageId rawMessage
                                      Origin =
                                        ProviderWireDecode.promptOriginOfMessage rawMessage
                                        |> Option.map decodePromptOrigin }))

                        match!
                            XTraceCapture.captureObservedMessagesWithReceipt
                                journal
                                (SessionId.create sessionId)
                                observations
                        with
                        | Ok captured ->
                            return
                                { RawMessages = rawMessages
                                  Current = captured.Current }
                        | Error(XTraceCaptureError.Refused reason)
                        | Error(XTraceCaptureError.StorageFailed reason) ->
                            return raiseFailClosed strengthFailFuse reason
                }
          CommitStrengthTrace =
            fun projectionSessionIdOpt traceState strengthReplayPlans ->
                match projectionSessionIdOpt with
                | Some _ ->
                    task {
                        do!
                            StrengthReplay.commitTracedAfterCapture
                                journal
                                strengthDurability
                                (raiseFailClosed strengthFailFuse)
                                traceState
                                strengthReplayPlans
                    }
                | None -> Task.FromResult()
          RefreshCompanionXTrace =
            fun projectionSessionIdOpt traceState ->
                let sessionId = projectionSessionIdOpt |> Option.defaultValue ""
                let found, companion = scope.Sessions.Companions.TryGetValue sessionId

                if found then
                    traceState |> Option.iter companion.RefreshXTrace
          ApplyCompanion =
            CompanionTransform.applyCompanionForOrdinaryMaterial
                scope.Sessions.Companions
                scope.Sessions.CompanionGate
                scope
                sessionPort
                journal
                (Some(fun bloggerId ->
                    // Register ownership + ActiveRun so idle→reconcile
                    // emits TerminalOutcome.Completed for this child.
                    wired.RegisterOwned(SessionId.value bloggerId)
                    wired.BindActiveRun bloggerId Role.Blogger None))
                (host.RootWorkspace.TryRead())
                (fun projectionSessionIdOpt outObj ->
                    ExplicitResumeSuppression.isCurrentMaterial outObj
                    || ExplicitResumeSuppression.isExplicitResumeBinding projectionSessionIdOpt outObj)
          ApplyXWire = XWire.applyTransform snapshotOpt journal scope
          FreezeProviderAttemptPlan = freezeProviderAttemptPlan
          ApplyEnforcerContinuation =
            fun projectionSessionIdOpt outObj ->
                task {
                    do!
                        EnforcerContinuation.applyContinuation
                            scope
                            journal
                            terminateSession
                            projectionSessionIdOpt
                            outObj
                }
          ApplyStrengthSpeculate = StrengthSpeculate.tryApply snapshotOpt journal strengthDurability scope
          InjectPairGuideline =
            fun projectionSessionIdOpt sessionStartedAt outObj ->
                task {
                    do!
                        PairProgrammingThoughtTransform.maybeInjectGuideline
                            journal
                            projectionSessionIdOpt
                            sessionStartedAt
                            clock
                            terminateSession
                            (languageFor projectionSessionIdOpt)
                            outObj
                }
          ProjectRequirementGrounding =
            RequirementGroundingTransform.projectOrTerminate journal workspaceDirectory terminateSession
          InjectBloggerChronicle =
            fun projectionSessionIdOpt outObj ->
                BloggerChronicleText.maybeInject
                    journal
                    projectionSessionIdOpt
                    (languageFor projectionSessionIdOpt)
                    outObj
          SanitizeMessages = HostMessageProjection.sanitizeOutputMessages }

    let defaultBranchCapabilities (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : TransformBranchCapabilities =
        let scope = boot.Scope
        let journal = boot.Journal
        let snapshotOpt = host.SnapshotOpt
        let wired = host.Wired

        { IsExplicitResume =
            fun projectionSessionIdOpt outObj ->
                ExplicitResumeSuppression.isCurrentMaterial outObj
                || ExplicitResumeSuppression.isExplicitResumeBinding projectionSessionIdOpt outObj
          RegisterOwned = wired.RegisterOwned
          ReplicaRuntime =
            fun projectionSessionIdOpt ->
                match projectionSessionIdOpt, scope.Strength.StrengthReplicaRuntime with
                | Some sessionId, Some runtime when runtime.IsReplica(SessionId.create sessionId) -> Some runtime
                | _ -> None
          ReplicaXWire =
            fun outObj ->
                task {
                    let! _ = XWire.applyTransform snapshotOpt journal scope outObj
                    return ()
                }
          ReplicaSanitize = HostMessageProjection.sanitizeOutputMessages
          ExplicitResumeSanitize = HostMessageProjection.sanitizeOutputMessages }

    let normalTransform
        (caps: NormalTransformCapabilities)
        (projectionSessionIdOpt: string option)
        (inObj: obj)
        (outObj: obj)
        : Task<unit> =
        task {
            // 1. SessionExecutionBinding.beginPhysicalProviderAttemptForTransform
            do! caps.BeginPhysicalProviderAttempt projectionSessionIdOpt outObj

            // 2. SessionStartedAtLedger.tryBindOrAbort
            let! sessionStartedAt = caps.BindSessionStartedAt projectionSessionIdOpt

            // 3. Relay projection cut + bounded baton injection. This MUST run
            // before every trace/compaction owner so retired raw history cannot
            // be reintroduced later in the composition.
            do! caps.ApplyRelayProjection projectionSessionIdOpt outObj

            // 4. StrengthReplay.applyBeforeXTrace
            let! strengthReplayPlans = caps.ApplyStrengthReplay projectionSessionIdOpt outObj

            // 5. XTraceCapture.captureObservedMessagesWithReceipt
            let! traceCapture = caps.CaptureXTraceMessages projectionSessionIdOpt outObj

            // 6. StrengthReplay.commitTracedAfterCapture
            do! caps.CommitStrengthTrace projectionSessionIdOpt traceCapture.Current strengthReplayPlans

            // 7. CompanionHost.RefreshXTrace
            caps.RefreshCompanionXTrace projectionSessionIdOpt traceCapture.Current

            // 8. applyCompanionForOrdinaryMaterial
            do! caps.ApplyCompanion projectionSessionIdOpt inObj outObj

            // 9. XWire.applyTransform. A selected prefix probe creates a
            // tentative cold horizon for this physical request; downstream
            // historical auxiliaries must not replay the old horizon into it.
            let! prefixHorizon = caps.ApplyXWire outObj

            // 10. SessionExecutionBinding.freezeProviderAttemptPlanForTransform
            // The transform sees the accepted user message only. Freeze the
            // exact request plan; a later public assistant observation owns
            // ProviderRunIdentity binding and ProviderStarted persistence.
            do! caps.FreezeProviderAttemptPlan projectionSessionIdOpt outObj

            // 11. EnforcerContinuation.applyContinuation
            do! caps.ApplyEnforcerContinuation projectionSessionIdOpt outObj

            if prefixHorizon = PrefixPresentationHorizon.Current then
                // 12. StrengthSpeculate.tryApply
                do! caps.ApplyStrengthSpeculate outObj

                // 13. PairProgrammingThoughtTransform.maybeInjectGuideline
                do! caps.InjectPairGuideline projectionSessionIdOpt sessionStartedAt outObj

                // 14. RequirementGroundingTransform.projectOrTerminate
                do! caps.ProjectRequirementGrounding projectionSessionIdOpt outObj

            // 15. BloggerChronicleText.maybeInject
            caps.InjectBloggerChronicle projectionSessionIdOpt outObj

            // 16. HostMessageProjection.sanitizeMessages
            caps.SanitizeMessages outObj

            ()
        }

    let createWithCaps
        (caps: NormalTransformCapabilities)
        (branches: TransformBranchCapabilities)
        : obj -> obj -> Task<unit> =
        fun (inObj: obj) (outObj: obj) ->
            task {
                let projectionSessionIdOpt =
                    projectionSessionIdFromMessages outObj
                    |> Option.orElseWith (fun () ->
                        if not (isNull inObj) && not (isNull inObj?sessionID) then
                            let sid = string inObj?sessionID
                            if String.IsNullOrWhiteSpace sid then None else Some sid
                        elif not (isNull inObj) && not (isNull inObj?sessionId) then
                            let sid = string inObj?sessionId
                            if String.IsNullOrWhiteSpace sid then None else Some sid
                        else
                            None)

                match determineTransformMode branches projectionSessionIdOpt outObj with
                | ExplicitResumeDisclosure ->
                    // CRASH-018: the exact /continue material stays disclosure-only
                    // for every provider step, including steps after tool results.
                    // The trailing marker is the direct path; the exact physical
                    // registry is the authoritative fallback when Host projection
                    // drops custom part metadata after chat.message.
                    // Do not reinterpret it through ordinary semantic transforms.
                    branches.ExplicitResumeSanitize outObj
                | StrengthReplica runtime ->
                    projectionSessionIdOpt |> Option.iter branches.RegisterOwned
                    // STRENGTH-004/009: Replica uses exactly one request-plan
                    // writer plus its mirror/K gate. XTrace, Manager narrative,
                    // Companion, Enforcer, Pair and Review are owner-only.
                    do! branches.ReplicaXWire outObj
                    do! caps.FreezeProviderAttemptPlan projectionSessionIdOpt outObj
                    let! handled = runtime.HandleTransform outObj
                    do failIfReplicaDecisionLost handled
                    branches.ReplicaSanitize outObj
                | Ordinary ->
                    projectionSessionIdOpt |> Option.iter branches.RegisterOwned
                    do! normalTransform caps projectionSessionIdOpt inObj outObj
            }

    /// Provider-facing transform composition: order only.
    /// Relay cut → Strength replay/trace → Companion/XWire → speculation;
    /// retired raw history is removed before any downstream context owner.
    let create (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : obj -> obj -> Task<unit> =
        let caps = defaultCapabilities boot host
        let branches = defaultBranchCapabilities boot host
        createWithCaps caps branches
