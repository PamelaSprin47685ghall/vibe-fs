namespace Wanxiangshu.Mission.Manager

open Wanxiangshu.Persistence.Journal.JournalOutcome
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Git
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Mission.Relay.OpenCode
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

module ManagerWorkflow =
    [<Literal>]
    let private assessPath = "runtime/manager-assess"

    [<Literal>]
    let private workPath = "runtime/manager-work"

    [<Literal>]
    let private finishPath = "runtime/manager-finish"

    let private roadId (sessionId: SessionId) =
        RoadId.create (SessionId.value sessionId)

    let private relayState (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Relay)

    let private currentView (journal: AgentJournal) (sessionId: SessionId) =
        relayState journal sessionId
        |> Option.bind (fun state -> Fold.view state (roadId sessionId))

    let private currentViewOf (journal: AgentJournal option) (sessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            relayState durable sessionId
            |> Option.bind (fun state -> Fold.view state (roadId sessionId)))

    /// The iteration ordinal as the provider reads it: "1" for the first
    /// Manager on this road, then one more per durable opening. A road with no
    /// view yet (first opening not yet committed) is still the first.
    let private iterationOrdinalText (journal: AgentJournal option) (sessionId: SessionId) =
        let ordinal =
            currentViewOf journal sessionId
            |> Option.map (fun road -> max 1 road.IterationOrdinal)
            |> Option.defaultValue 1

        string ordinal

    let private isRetiredObservation
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        =
        journal
        |> Option.bind (fun durable -> currentView durable sessionId)
        |> Option.exists (fun road -> Set.contains (ProviderRunIdentity.value providerRun) road.RetiredProviderRunIds)

    let private lifecycleWork (directory: string option) (sessionId: string) =
        match directory |> Option.map Wanxiangshu.Persistence.Journal.RuntimePath.gitCommonDir with
        | None -> None
        | Some dir when System.String.IsNullOrWhiteSpace dir -> None
        | Some dir ->
            match WorkspaceEventStore.tryCurrent dir with
            | None -> None
            | Some held ->
                match held.TryCurrent "Lifecycle" with
                | None -> None
                | Some current ->
                    let works = unbox<Map<string, Wanxiangshu.Mission.Lifecycle.LifecycleWork>> current
                    Map.tryFind sessionId works

    // Algorithm H reads the opening sequence plus tenure, review outcome, and confirmation.
    // S1–S3 do not enter the S4 resource match. Certificates are not an input.
    let private resourceForCurrentAction (directory: string option) (sessionId: SessionId) (road: RoadView) =
        match lifecycleWork directory (SessionId.value sessionId) with
        | None -> None
        | Some work when work.Sequence <> Wanxiangshu.Mission.Lifecycle.WorkSequence.Execution -> None
        | Some work ->
            match road.ActiveIncumbency, work.Active with
            | None, _
            | Some _, None -> None
            | Some _, Some active when work.CurrentRelayed && active.Review.IsNone -> None
            | Some _, Some active when active.Review.IsNone && active.Confirmed.IsSome -> None
            | Some _, Some active when active.Review.IsNone -> Some assessPath
            | Some _, Some active when active.Confirmed.IsNone ->
                match active.Review with
                | Some review when review.Outcome = Wanxiangshu.Mission.Lifecycle.ReviewOutcome.Stayed ->
                    Some workPath
                | Some review when review.Outcome = Wanxiangshu.Mission.Lifecycle.ReviewOutcome.Promoted ->
                    Some finishPath
                | _ -> None
            | Some _, Some active ->
                match active.Review with
                | Some review when
                    review.Outcome = Wanxiangshu.Mission.Lifecycle.ReviewOutcome.Promoted
                    || review.Outcome = Wanxiangshu.Mission.Lifecycle.ReviewOutcome.Stayed ->
                    Some finishPath
                | _ -> None


    let private activeResourcePath (directory: string option) (sessionId: SessionId) (view: RoadView option) =
        view |> Option.bind (resourceForCurrentAction directory sessionId)

    let private sendNudge
        (quiescence: ISessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (journal: AgentJournal)
        (turn: ReconciledTurn)
        (resourcePath: string)
        =
        let substitutions =
            if resourcePath = assessPath then
                Map [ "ordinal", iterationOrdinalText (Some journal) turn.SessionId ]
            elif resourcePath = finishPath then
                let confirmed =
                    match lifecycleWork turn.Directory (SessionId.value turn.SessionId) with
                    | Some work ->
                        match work.Active with
                        | Some active -> active.Confirmed.IsSome
                        | None -> false
                    | None -> false

                let preferEnglish =
                    match ProviderLanguageBinding.readGlobalPreference () with
                    | ProviderLanguage.English -> true
                    | _ -> false

                let closeout =
                    if preferEnglish then
                        if confirmed then
                            "Confirmation is already recorded. A later deliver with a different call may close out. Closing is not written in this prompt."
                        else
                            "This call only records confirmation. It does not end."
                    elif confirmed then
                        "确认已经落下。下一次不同调用的 deliver 才收口。收束不写在本提示里。"
                    else
                        "这一次调用只落下确认，不结束。"

                Map [ "closeout", closeout ]
            else
                Map.empty

        HostSessionNudge.trySendIdleGateContinuation
            quiescence
            permit
            sessionPort
            rootWorkspace
            turn.SessionId
            (ProviderProse.documentFor turn.SessionId resourcePath substitutions)
            PromptAuthority.ContinuationKind.ManagerGuard
            turn.Directory
            (Some journal)
            (resourcePath + ":" + ProviderRunIdentity.value turn.ProviderRun)
            turn.ProviderRun
            PromptDispatcher.AwaitMode.Detached

    let private scheduleNudge
        (quiescence: ISessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (journal: AgentJournal)
        (turn: ReconciledTurn)
        =
        task {
            let view = currentView journal turn.SessionId

            match activeResourcePath turn.Directory turn.SessionId view with
            | Some resourcePath ->
                // Durable PromptAuthority gate is the sole dedupe/source of truth:
                // Sent/AlreadyAdmitted/Retired are settled no-op success, Failed stays nonfatal.
                let! _ = sendNudge quiescence permit sessionPort rootWorkspace journal turn resourcePath
                return ()
            | None -> return ()
        }
        :> Task

    let private mergeWithInvalidation (opening: IncumbencyOpening) invalidationEvents =
        if List.isEmpty invalidationEvents then
            opening.Transaction
        else
            RelayTransaction.create (invalidationEvents @ RelayTransaction.events opening.Transaction)
            |> Result.defaultValue opening.Transaction

    let private requiresContinuation (road: RoadView) (retirement: RetirementSummary) =
        match retirement.Outcome with
        | RetirementOutcome.Relayed -> true
        | RetirementOutcome.Delivered -> false

    let private isDeliveredRetirement (retirement: RetirementSummary) =
        match retirement.Outcome with
        | RetirementOutcome.Delivered -> true
        | RetirementOutcome.Relayed -> false

    let private tryAcceptedRoadContext (durable: AgentJournal) sidText =
        let sessionId = SessionId.create sidText
        let roadId = RoadId.create sidText
        let snapshot = AgentJournal.snapshot durable

        let roadOpt =
            AgentProjection.tryFind sessionId snapshot.AgentProjections
            |> Option.bind (fun (s: SessionAgentProjection) -> s.Relay)
            |> Option.bind (fun (r: RelayState) -> Fold.view r roadId)

        match roadOpt with
        | Some road ->
            road.LatestRetirement
            |> Option.filter isDeliveredRetirement
            |> Option.map (fun retirement ->
                durable, sessionId, roadId, road.AuthorityRevision, retirement, road.ActiveIncumbency.IsNone)
        | _ -> None

    let private loopContextFor (durable: AgentJournal) sidText =
        let sessionId = SessionId.create sidText
        let roadId = RoadId.create sidText
        let snapshot = AgentJournal.snapshot durable

        let roadOpt =
            AgentProjection.tryFind sessionId snapshot.AgentProjections
            |> Option.bind (fun (s: SessionAgentProjection) -> s.Relay)
            |> Option.bind (fun (r: RelayState) -> Fold.view r roadId)

        match roadOpt with
        | Some road ->
            road.LatestRetirement
            |> Option.filter (requiresContinuation road)
            |> Option.map (fun retirement ->
                durable, sessionId, roadId, road.AuthorityRevision, retirement, road.ActiveIncumbency.IsNone)
        | _ -> None

    let private decideLoopContext journal sessionIdTextOpt =
        match sessionIdTextOpt, journal with
        | Some sidText, Some durable when not (System.String.IsNullOrWhiteSpace sidText) ->
            loopContextFor durable sidText
        | _ -> None

    let private tryBuildAcceptedOpening
        durable
        workspaceDirectory
        sessionId
        roadId
        authorityRevision
        (retirement: RetirementSummary)
        =
        ignore durable
        ignore workspaceDirectory
        ignore sessionId
        ignore roadId
        ignore authorityRevision
        ignore retirement
        None

    let private decideLoopOpening journal (workspaceDirectory: string option) sessionIdTextOpt =
        sessionIdTextOpt
        |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
        |> Option.bind (fun sessionIdText ->
            journal
            |> Option.bind (fun durable ->
                match tryAcceptedRoadContext durable sessionIdText with
                | Some(_, sessionId, roadId, authorityRevision, retirement, true) ->
                    tryBuildAcceptedOpening durable workspaceDirectory sessionId roadId authorityRevision retirement
                | _ -> None))

    let private decideInitialOpening durable workspaceDirectory sessionId sessionIdText =
        let snapshot = AgentJournal.snapshot durable
        let roadId = RoadId.create sessionIdText

        let roadView =
            AgentProjection.tryFind sessionId snapshot.AgentProjections
            |> Option.bind (fun (s: SessionAgentProjection) -> s.Relay)
            |> Option.bind (fun (r: RelayState) -> Fold.view r roadId)

        let profileOpt =
            PromptAuthorityProjectionQueries.activeProfile sessionId snapshot.AgentProjections

        ignore durable
        ignore workspaceDirectory
        ignore sessionId
        ignore sessionIdText
        ignore roadView
        ignore profileOpt
        None

    let private decideOpeningAction journal (workspaceDirectory: string option) sessionIdTextOpt =
        match decideLoopOpening journal workspaceDirectory sessionIdTextOpt with
        | Some action -> Some action
        | None ->
            sessionIdTextOpt
            |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
            |> Option.bind (fun sessionIdText ->
                journal
                |> Option.bind (fun durable ->
                    let sessionId = SessionId.create sessionIdText
                    decideInitialOpening durable workspaceDirectory sessionId sessionIdText))

    let ensureManagerRoadOpened
        (journal: AgentJournal option)
        (workspaceDirectory: string option)
        (store: Wanxiangshu.Persistence.EventStore.IEventStore option)
        (observedHead: int64 option)
        (sessionIdTextOpt: string option)
        (providerRunIdOpt: ProviderRunIdentity option)
        : Task<unit> =
        task {
            ignore journal
            ignore providerRunIdOpt
            match store, sessionIdTextOpt, workspaceDirectory with
            | None, _, _
            | _, None, _
            | _, _, None -> return ()
            | Some held, Some sessionId, Some root ->
                let firstLine = None
                match
                    Wanxiangshu.Mission.Lifecycle.LifecycleKernel.openWork
                        HostDigest.sha256Hex
                        Wanxiangshu.Mission.Lifecycle.OpeningKind.ManagerDirect
                        firstLine
                        sessionId
                        root
                        false
                        observedHead
                with
                | Wanxiangshu.Mission.Lifecycle.KernelDecision.Write(events, _) ->
                    let! _ = Wanxiangshu.Mission.Lifecycle.LifecycleKernel.commit held sessionId events
                    return ()
                | Wanxiangshu.Mission.Lifecycle.KernelDecision.Refuse _
                | Wanxiangshu.Mission.Lifecycle.KernelDecision.Done _ -> return ()
        }

    let private deliverLoopPrompt
        sessionPort
        rootWorkspace
        workspaceDirectory
        (durable: AgentJournal)
        sessionId
        (retirement: RetirementSummary)
        =
        task {
            match lifecycleWork workspaceDirectory (SessionId.value sessionId) with
            | Some work when work.Sequence = Wanxiangshu.Mission.Lifecycle.WorkSequence.Execution -> ()
            | _ -> return ()

            let loopPromptText =
                ProviderProse.documentFor
                    sessionId
                    "runtime/manager-assess"
                    (Map [ "ordinal", iterationOrdinalText (Some durable) sessionId ])

            let terminalRun = ProviderRunIdentity.create retirement.ProjectionCut.ProviderRunId

            match!
                HostSessionNudge.trySendGateContinuation
                    sessionPort
                    rootWorkspace
                    sessionId
                    loopPromptText
                    PromptAuthority.ContinuationKind.ManagerGuard
                    workspaceDirectory
                    (Some durable)
                    (ManagerLoopGate.gateKind retirement.Id)
                    terminalRun
            with
            | HostSessionNudge.GateContinuationOutcome.Sent _
            | HostSessionNudge.GateContinuationOutcome.AlreadyAdmitted -> return ()
            | HostSessionNudge.GateContinuationOutcome.Retired ->
                return
                    invalidOp (
                        sprintf
                            "MANAGER-LOOP-003: loop gate retired for retirement %s"
                            (RetirementId.value retirement.Id)
                    )
            | HostSessionNudge.GateContinuationOutcome.Failed error ->
                return invalidOp (sprintf "MANAGER-LOOP-003: loop gate nudge failed: %s" error)
        }

    let private deliverLoopContext
        sessionPort
        rootWorkspace
        workspaceDirectory
        (context: AgentJournal * SessionId * RoadId * AuthorityRevision * RetirementSummary * bool)
        =
        task {
            let durable, sessionId, roadId, authorityRevision, retirement, needsOpening =
                context

            ignore needsOpening
            ignore roadId
            ignore authorityRevision

            do! deliverLoopPrompt sessionPort rootWorkspace workspaceDirectory durable sessionId retirement
        }

    let maybeDeliverLoop
        sessionPort
        rootWorkspace
        (journal: AgentJournal option)
        (workspaceDirectory: string option)
        (sessionIdTextOpt: string option)
        : Task<unit> =
        task {
            match decideLoopContext journal sessionIdTextOpt with
            | Some context -> do! deliverLoopContext sessionPort rootWorkspace workspaceDirectory context
            | None -> return ()
        }

    let continueAfterRetiredAttempt
        sessionPort
        rootWorkspace
        (journal: AgentJournal option)
        (workspaceDirectory: string option)
        (stopRetiredAttempt: SessionId -> Task<unit>)
        (sessionId: SessionId)
        : Task<unit> =
        task {
            let loopContext = decideLoopContext journal (Some(SessionId.value sessionId))

            do! stopRetiredAttempt sessionId

            match
                loopContext
                |> Option.orElseWith (fun () -> decideLoopContext journal (Some(SessionId.value sessionId)))
            with
            | Some context -> do! deliverLoopContext sessionPort rootWorkspace workspaceDirectory context
            | None -> return ()
        }

    let observeIdle
        (quiescence: ISessionQuiescenceGate)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (journal: AgentJournal option)
        (context: ReconciledTurnContext)
        : Task =
        match journal, context.Failure, context.Turn.Outcome, context.Quiescence with
        | Some durable, None, ReconcileProgram.TurnCompleted, Some permit ->
            scheduleNudge quiescence permit sessionPort rootWorkspace durable context.Turn
        | _ -> Task.FromResult()

    let observe
        (quiescence: ISessionQuiescenceGate)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (journal: AgentJournal option)
        (observeOrdinary: ReconciledTurnContext -> Task)
        (context: ReconciledTurnContext)
        : Task =
        match
            isRetiredObservation journal context.Turn.SessionId context.Turn.ProviderRun,
            context.Failure,
            context.Turn.Outcome
        with
        | true, _, _ -> Task.FromResult()
        | false, Some _, _ -> observeOrdinary context
        | false, None, ReconcileProgram.TurnInProgress -> Task.FromResult()
        // provider-attempt-recovery-008: an unfinished manager turn is content damage, not a
        // reason to stop. It earns the same bounded Interaction Repair as every
        // other repairable role instead of being silently dropped.
        | false, None, ReconcileProgram.TurnNeedsContinuation _ -> observeOrdinary context
        | false, None, ReconcileProgram.TurnCompleted ->
            observeIdle quiescence sessionPort rootWorkspace journal context
        | false, None, _ -> observeOrdinary context
