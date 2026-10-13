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
    let private resourceForCurrentAction (directory: string option) (sessionId: SessionId) =
        match lifecycleWork directory (SessionId.value sessionId) with
        | None -> None
        | Some work when work.Sequence <> Wanxiangshu.Mission.Lifecycle.WorkSequence.Execution -> None
        | Some work ->
            match work.Active with
            | None -> None
            | Some active when work.CurrentRelayed && active.Review.IsNone -> None
            | Some active when active.Review.IsNone && active.Confirmed.IsSome -> None
            | Some active when active.Review.IsNone -> Some assessPath
            | Some active when active.Confirmed.IsNone ->
                match active.Review with
                | Some review when review.Outcome = Wanxiangshu.Mission.Lifecycle.ReviewOutcome.Stayed ->
                    Some workPath
                | Some review when review.Outcome = Wanxiangshu.Mission.Lifecycle.ReviewOutcome.Promoted ->
                    Some finishPath
                | _ -> None
            | Some active ->
                match active.Review with
                | Some review when
                    review.Outcome = Wanxiangshu.Mission.Lifecycle.ReviewOutcome.Promoted
                    || review.Outcome = Wanxiangshu.Mission.Lifecycle.ReviewOutcome.Stayed ->
                    Some finishPath
                | _ -> None


    let private activeResourcePath (directory: string option) (sessionId: SessionId) =
        resourceForCurrentAction directory sessionId

    /// The closeout instruction substituted into `runtime/manager-finish`. The
    /// two-stage deliver is the point: the first call only records confirmation,
    /// a later call with a different tool call is what closes out.
    let finishCloseoutText (confirmed: bool) : string =
        match ProviderLanguageBinding.readGlobalPreference () with
        | ProviderLanguage.English ->
            if confirmed then
                "Confirmation is already recorded. A later deliver with a different call may close out. Closing is not written in this prompt."
            else
                "This call only records confirmation. It does not end."
        | _ ->
            if confirmed then
                "确认已经落下。下一次不同调用的 deliver 才收口。收束不写在本提示里。"
            else
                "这一次调用只落下确认，不结束。"

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

                Map [ "closeout", finishCloseoutText confirmed ]
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
            match activeResourcePath turn.Directory turn.SessionId with
            | Some resourcePath ->
                // Durable PromptAuthority gate is the sole dedupe/source of truth:
                // Sent/AlreadyAdmitted/Retired are settled no-op success, Failed stays nonfatal.
                let! _ = sendNudge quiescence permit sessionPort rootWorkspace journal turn resourcePath
                return ()
            | None -> return ()
        }
        :> Task

    /// Algorithm H8 entry two: after a Relayed retirement on an S4 work whose
    /// successor has not been reviewed yet, the successor prompt is the first
    /// visible instruction. The retired record's cut names the provider run.
    let private successorPromptContext
        (workspaceDirectory: string option)
        (sessionIdText: string)
        : (string * string) option =
        match lifecycleWork workspaceDirectory sessionIdText with
        | Some work when
            work.Sequence = Wanxiangshu.Mission.Lifecycle.WorkSequence.Execution && work.CurrentRelayed
            ->
            match work.Active with
            | Some active when active.Review.IsNone ->
                work.Retirements
                |> List.tryFindBack (fun record ->
                    record.Outcome = Wanxiangshu.Mission.Lifecycle.RetirementOutcome.Relayed)
                |> Option.bind (fun record ->
                    record.ProjectionCut
                    |> Option.map (fun cut -> record.RetirementId, cut.ProviderRunId))
            | _ -> None
        | _ -> None

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
        (retirementId: string)
        (terminalProviderRunId: string)
        =
        task {
            let loopPromptText =
                ProviderProse.documentFor
                    sessionId
                    "runtime/manager-assess"
                    (Map [ "ordinal", iterationOrdinalText (Some durable) sessionId ])

            let terminalRun = ProviderRunIdentity.create terminalProviderRunId

            match!
                HostSessionNudge.trySendGateContinuation
                    sessionPort
                    rootWorkspace
                    sessionId
                    loopPromptText
                    PromptAuthority.ContinuationKind.ManagerGuard
                    workspaceDirectory
                    (Some durable)
                    (ManagerLoopGate.gateKind (RetirementId.create retirementId))
                    terminalRun
            with
            | HostSessionNudge.GateContinuationOutcome.Sent _
            | HostSessionNudge.GateContinuationOutcome.AlreadyAdmitted -> return ()
            | HostSessionNudge.GateContinuationOutcome.Retired ->
                return
                    invalidOp (
                        sprintf
                            "MANAGER-LOOP-003: loop gate retired for retirement %s"
                            retirementId
                    )
            | HostSessionNudge.GateContinuationOutcome.Failed error ->
                return invalidOp (sprintf "MANAGER-LOOP-003: loop gate nudge failed: %s" error)
        }

    let private deliverSuccessorPrompt
        sessionPort
        rootWorkspace
        workspaceDirectory
        (durable: AgentJournal)
        (sessionId: SessionId)
        =
        task {
            match successorPromptContext workspaceDirectory (SessionId.value sessionId) with
            | Some(retirementId, providerRunId) ->
                do!
                    deliverLoopPrompt
                        sessionPort
                        rootWorkspace
                        workspaceDirectory
                        durable
                        sessionId
                        retirementId
                        providerRunId
            | None -> return ()
        }

    let maybeDeliverLoop
        sessionPort
        rootWorkspace
        (journal: AgentJournal option)
        (workspaceDirectory: string option)
        (sessionIdTextOpt: string option)
        : Task<unit> =
        task {
            match journal, sessionIdTextOpt with
            | Some durable, Some sidText when not (System.String.IsNullOrWhiteSpace sidText) ->
                do! deliverSuccessorPrompt sessionPort rootWorkspace workspaceDirectory durable (SessionId.create sidText)
            | _ -> return ()
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
            let sendPrompt =
                journal
                |> Option.map (fun durable ->
                    fun () -> deliverSuccessorPrompt sessionPort rootWorkspace workspaceDirectory durable sessionId)

            do! stopRetiredAttempt sessionId

            match sendPrompt with
            | Some send -> do! send ()
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
