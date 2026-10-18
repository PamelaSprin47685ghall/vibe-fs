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
open Wanxiangshu.Mission.Work
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

    let private lifecycleWork (directory: string option) (sessionId: string) =
        directory
        |> Option.bind (fun workspace ->
            let commonDir = Wanxiangshu.Persistence.Journal.RuntimePath.stateRoot workspace
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected commonDir sessionId)

    /// The iteration ordinal as the provider reads it: "1" for the first
    /// Manager on this road, then one more per durable opening. A road with no
    /// view yet (first opening not yet committed) is still the first.
    let private iterationOrdinalText (directory: string option) (sessionId: SessionId) =
        let ordinal =
            lifecycleWork directory (SessionId.value sessionId)
            |> Option.map (fun work ->
                let retired = work.RoundCount
                max 1 (retired + if work.ActiveId.IsSome then 1 else 0))
            |> Option.defaultValue 1

        string ordinal

    /// True when this provider run already produced one of the work's
    /// retirements: the run is retired and its later observations are stale.
    let private isRetiredObservation
        (directory: string option)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        =
        lifecycleWork directory (SessionId.value sessionId)
        |> Option.exists (fun work -> List.contains (ProviderRunIdentity.value providerRun) work.RetiredRuns)

    /// An unreviewed S4 tenure whose confirmation already landed cannot
    /// happen through the library's own gate; treat it as no action.
    let private unreviewedResource (confirmed: bool) =
        if confirmed then None else Some assessPath

    /// The resource for a reviewed, unconfirmed S4 tenure, by review outcome.
    let private reviewedResource (outcome: string option) =
        match outcome with
        | Some "Stayed" -> Some workPath
        | Some "Promoted" -> Some finishPath
        | _ -> None

    // Algorithm H reads the opening sequence plus tenure, review outcome, and confirmation.
    // S1–S3 do not enter the S4 resource match. Certificates are not an input.
    let private resourceForCurrentAction (directory: string option) (sessionId: SessionId) =
        match lifecycleWork directory (SessionId.value sessionId) with
        | None -> None
        | Some work when work.Sequence <> [ "S4" ] -> None
        | Some work when work.ActiveId.IsNone -> None
        | Some work when work.Reviewed |> not -> unreviewedResource work.Confirmed
        | Some work when not work.Confirmed -> reviewedResource work.ReviewOutcome
        | Some _ -> Some finishPath


    let private activeResourcePath (directory: string option) (sessionId: SessionId) =
        resourceForCurrentAction directory sessionId

    /// The closeout instruction substituted into `runtime/manager-finish`. The
    /// two-stage deliver is the point: the first call only records confirmation,
    /// a later call with a different tool call is what closes out.
    let finishCloseoutText (confirmed: bool) : string =
        match ProviderLanguageBinding.readGlobalPreference () with
        | ProviderLanguage.English when confirmed ->
            "Confirmation is already recorded. A later deliver with a different call may close out. Closing is not written in this prompt."
        | ProviderLanguage.English -> "This call only records confirmation. It does not end."
        | _ when confirmed -> "确认已经落下。下一次不同调用的 deliver 才收口。收束不写在本提示里。"
        | _ -> "这一次调用只落下确认，不结束。"

    /// The closeout substitution reads the confirmation bit from the work the
    /// closeout targets. A missing work is still unconfirmed.
    let private closeoutSubstitutions (turn: ReconciledTurn) =
        let confirmed =
            lifecycleWork turn.Directory (SessionId.value turn.SessionId)
            |> Option.exists (fun work -> work.Confirmed)

        Map [ "closeout", finishCloseoutText confirmed ]

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
                Map [ "ordinal", iterationOrdinalText turn.Directory turn.SessionId ]
            elif resourcePath = finishPath then
                closeoutSubstitutions turn
            else
                Map.empty

        HostSessionNudge.trySendIdleGateContinuation
            quiescence
            permit
            sessionPort
            rootWorkspace
            turn.SessionId
            (ProviderProse.documentFor turn.SessionId "work/manager-continue" Map.empty
             + "\n\n"
             + ProviderProse.documentFor turn.SessionId resourcePath substitutions)
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
            // 没读到工作不是完成证据；提醒继续核对任务，而不是悄悄放行自然结束。
            let resourcePath =
                activeResourcePath turn.Directory turn.SessionId |> Option.defaultValue workPath

            let! outcome = sendNudge quiescence permit sessionPort rootWorkspace journal turn resourcePath

            match outcome with
            | HostSessionNudge.IdleContinuationOutcome.Failed error
            | HostSessionNudge.IdleContinuationOutcome.NotSent error ->
                return invalidOp (sprintf "Manager continuation could not be delivered: %s" error)
            | _ -> return ()
        }
        :> Task

    /// Algorithm H8 entry two: an S4 work whose open tenure has not been reviewed
    /// yet starts from the successor prompt, and the last committed retirement is
    /// the occasion: a Relayed retirement opened the successor itself, a Delivered
    /// retirement is the cut Change handed the same Work over from after a machine
    /// conflict or a rebase that invalidated the candidate
    /// (change-integration-005/013). Either way the retirement id names the cut
    /// and the committed retirement fence carries the provider run that made it;
    /// no run identity is invented, and a work with no retirement (the first
    /// tenure) gets its authority from the job's own prompt, not from here.
    let private successorPromptContext
        (workspaceDirectory: string option)
        (sessionIdText: string)
        : (string * string) option =
        lifecycleWork workspaceDirectory sessionIdText
        |> Option.bind (fun work ->
            match work.Sequence, work.ActiveId, List.tryLast work.Retirements with
            | [ "S4" ], Some _, Some retirement when not work.Reviewed ->
                Some(retirement.RetirementId, retirement.Fence.ProviderRunId)
            | _ -> None)

    /// One durable opening at an observed head. DurableWorkHost owns race
    /// reconciliation: a conflicting or unknown append is accepted only when
    /// a reread proves the same Manager work now exists.
    let private openManagerWorkAtHead (sessionId: string) (directory: string) (head: int64) : unit =
        // A plugin may enter from a subdirectory. The work belongs to the
        // actual worktree root, not that entry directory or the common Git dir.
        let root =
            Wanxiangshu.Git.GitSubject.execIn directory [| "rev-parse"; "--show-toplevel" |]
            |> fun path -> path.TrimEnd('\r', '\n')

        if System.String.IsNullOrWhiteSpace root then
            invalidOp "Manager lifecycle opening failed: Git did not identify a worktree root"

        let gitCommonDir = Wanxiangshu.Persistence.Journal.RuntimePath.stateRoot root

        match
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.ensureOpened
                gitCommonDir
                sessionId
                (sessionId + ":open:" + string head)
                root
                (Wanxiangshu.Mission.Lifecycle.DurableWorkHost.executionSequence ())
                sessionId
                (float head)
        with
        | Ok _ -> ()
        | Error reason -> invalidOp ("Manager lifecycle opening failed: " + reason)

    let ensureManagerWorkOpened
        (workspaceDirectory: string option)
        (observedHead: int64 option)
        (sessionIdTextOpt: string option)
        : Task<unit> =
        task {
            match sessionIdTextOpt, workspaceDirectory with
            | None, _
            | _, None -> return ()
            | Some sessionId, Some root -> observedHead |> Option.iter (openManagerWorkAtHead sessionId root)
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
                    (Map [ "ordinal", iterationOrdinalText workspaceDirectory sessionId ])

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
                return invalidOp (sprintf "MANAGER-LOOP-003: loop gate retired for retirement %s" retirementId)
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
                do!
                    deliverSuccessorPrompt
                        sessionPort
                        rootWorkspace
                        workspaceDirectory
                        durable
                        (SessionId.create sidText)
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
        | Some durable, _, _, _ when
            UserQuestionJournal.suppressesTurn
                durable
                context.Turn.SessionId
                context.Turn.AuthorityRootUserMessageId
                context.Turn.ProviderRun
            ->
            Task.FromResult()
        | _, _, _, _ when isRetiredObservation context.Turn.Directory context.Turn.SessionId context.Turn.ProviderRun ->
            Task.FromResult()
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
            isRetiredObservation context.Turn.Directory context.Turn.SessionId context.Turn.ProviderRun,
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
