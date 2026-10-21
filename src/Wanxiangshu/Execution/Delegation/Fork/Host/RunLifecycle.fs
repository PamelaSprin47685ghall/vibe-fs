namespace Wanxiangshu.Execution.Delegation.Fork.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.OpenCode
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Foundation.TerminalSignal
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Programming.Js
open Fable.Core

/// Per-run terminal lifecycle for HostForkRuntime: install, complete, fail.
module HostForkRunLifecycle =

    [<Emit("console.error($0, $1)")>]
    let private logError (prefix: string) (message: string) : unit = jsNative

    /// The delivered view of one terminal, resolved on demand.
    ///
    /// The resolution is deferred on purpose. A terminal that does not
    /// address this run must never spend the live run's material, so the
    /// view is read only after the ownership guards have passed — not when
    /// the terminal arrives. The resolution never throws: a faulting read is
    /// an empty view (EXEC-006).
    type ViewResolution = unit -> Task<string option>

    [<RequireQualifiedAccess>]
    type AgentOwnerDispatchOutcome =
        | Accepted of physicalUserMessageId: PhysicalUserMessageId * authorityRoot: AuthorityRootUserMessageId
        | AcceptanceUncertain of string
        | Rejected of string

    let private admittedWork durable parentId agentId childId authorityRoot =
        HandleProjection.tryAdmittedWork
            { Handle = HandleController.agentHandle agentId
              ChildSessionId = childId
              AuthorityRoot = authorityRoot }
            (AgentJournal.handleProjection durable parentId)

    let private acceptedWorkOutcome durable parentId childId root physical (binding: HandleRecord) =
        match
            HandleProjection.tryAdmittedWork
                { Handle = binding.Handle
                  ChildSessionId = childId
                  AuthorityRoot = root }
                (AgentJournal.handleProjection durable parentId)
        with
        | Ok _ -> AgentOwnerDispatchOutcome.Accepted(physical, root)
        | Error _ ->
            AgentOwnerDispatchOutcome.AcceptanceUncertain
                "physical acceptance exists; exact work admission is not established"

    let private acceptedBindingOutcome durable parentId childId root physical =
        let projection = (AgentJournal.snapshot durable).AgentProjections

        match Map.tryFind childId projection.HandleByChildSession with
        | Some binding -> acceptedWorkOutcome durable parentId childId root physical binding
        | None -> AgentOwnerDispatchOutcome.AcceptanceUncertain "physical acceptance exists; child binding is missing"

    let private acceptedOutcome
        durable
        childId
        identitySeed
        (onAccepted: ContinuationAcceptanceObserver)
        (evidence: PromptAuthority.AcceptedDispatch)
        =
        onAccepted.Notify evidence.PhysicalUserMessageId

        let root =
            PhysicalUserMessageId.promoteToAuthorityRoot evidence.PhysicalUserMessageId

        match PromptAuthority.identitySeedOwner identitySeed with
        | Some(parentId, _, _) -> acceptedBindingOutcome durable parentId childId root evidence.PhysicalUserMessageId
        | None -> AgentOwnerDispatchOutcome.Rejected "child assignment has no exact owner identity"

    let issueCurrentOwnerIdentitySeed
        (journal: AgentJournal option)
        (ownerSessionId: SessionId)
        (childAgent: string)
        : Result<PromptAuthority.IdentitySeed, string> =
        match journal with
        | None -> Error "No journal: an AgentOwnerRoot identity seed cannot be issued"
        | Some durable ->
            PromptAuthorityProjectionQueries.issueCurrentOwnerIdentitySeed
                (AgentJournal.snapshot durable).AgentProjections
                ownerSessionId
                childAgent

    [<RequireQualifiedAccess>]
    type private DurableDispatchObservation =
        | Accepted of PromptAuthority.AcceptedDispatch
        | Pending of PromptAuthority.PromptClaim
        | IdentityMismatch
        | Dispatchable

    let private pendingDispatchObservation childId payloadDigest identitySeed projections =
        match PromptAuthorityProjectionQueries.pendingDispatchClaim childId payloadDigest projections with
        | Some claim when claim.IdentitySeed = identitySeed -> DurableDispatchObservation.Pending claim
        | Some _ -> DurableDispatchObservation.IdentityMismatch
        | None ->
            raise (
                InvalidOperationException(
                    "PromptAuthority projection reported Pending without the matching durable claim"
                )
            )

    let private durableDispatchObservation
        (durable: AgentJournal)
        (childId: SessionId)
        (payloadDigest: string)
        (identitySeed: PromptAuthority.IdentitySeed)
        =
        let projections = (AgentJournal.snapshot durable).AgentProjections

        match PromptAuthorityProjectionQueries.dispatchStatusFor childId payloadDigest projections with
        | PromptAuthorityProjectionQueries.DispatchStatus.Accepted evidence when evidence.IdentitySeed = identitySeed ->
            DurableDispatchObservation.Accepted evidence
        | PromptAuthorityProjectionQueries.DispatchStatus.Accepted _ -> DurableDispatchObservation.IdentityMismatch
        | PromptAuthorityProjectionQueries.DispatchStatus.Pending ->
            pendingDispatchObservation childId payloadDigest identitySeed projections
        | PromptAuthorityProjectionQueries.DispatchStatus.Dispatchable -> DurableDispatchObservation.Dispatchable

    let private classifyPendingSend
        (durable: AgentJournal)
        (childId: SessionId)
        (payloadDigest: string)
        (identitySeed: PromptAuthority.IdentitySeed)
        (claim: PromptAuthority.PromptClaim)
        (onAccepted: ContinuationAcceptanceObserver)
        (accepted: PromptAuthority.AcceptedDispatch -> AgentOwnerDispatchOutcome)
        (error: string)
        =
        let registration =
            PromptPhysicalAcceptance.register claim.PromptKey onAccepted.Notify

        try
            onAccepted.AttachDisposable registration
        with error ->
            registration.Dispose()
            raise error

        match durableDispatchObservation durable childId payloadDigest identitySeed with
        | DurableDispatchObservation.Accepted evidence ->
            registration.Dispose()
            accepted evidence
        | DurableDispatchObservation.Pending _ -> AgentOwnerDispatchOutcome.AcceptanceUncertain error
        | DurableDispatchObservation.IdentityMismatch ->
            registration.Dispose()
            AgentOwnerDispatchOutcome.Rejected "Durable child dispatch identity witness does not match this owner run"
        | DurableDispatchObservation.Dispatchable ->
            registration.Dispose()
            AgentOwnerDispatchOutcome.Rejected error

    let private classifySendError
        (durable: AgentJournal)
        (childId: SessionId)
        (identitySeed: PromptAuthority.IdentitySeed)
        (prompt: string)
        (onAccepted: ContinuationAcceptanceObserver)
        (error: string)
        =
        let payloadDigest = HostDigest.sha256Hex prompt

        let accepted = acceptedOutcome durable childId identitySeed onAccepted

        match durableDispatchObservation durable childId payloadDigest identitySeed with
        | DurableDispatchObservation.Accepted evidence -> accepted evidence
        | DurableDispatchObservation.IdentityMismatch ->
            AgentOwnerDispatchOutcome.Rejected "Durable child dispatch identity witness does not match this owner run"
        | DurableDispatchObservation.Dispatchable -> AgentOwnerDispatchOutcome.Rejected error
        | DurableDispatchObservation.Pending claim ->
            classifyPendingSend durable childId payloadDigest identitySeed claim onAccepted accepted error

    let private classifySendSuccess durable childId identitySeed prompt onAccepted =
        match durableDispatchObservation durable childId (HostDigest.sha256Hex prompt) identitySeed with
        | DurableDispatchObservation.Accepted evidence ->
            acceptedOutcome durable childId identitySeed onAccepted evidence
        | DurableDispatchObservation.Pending _ ->
            AgentOwnerDispatchOutcome.AcceptanceUncertain "Prompt submitted but physical acceptance unconfirmed"
        | DurableDispatchObservation.IdentityMismatch ->
            AgentOwnerDispatchOutcome.Rejected "Durable child dispatch identity witness does not match this owner run"
        | DurableDispatchObservation.Dispatchable ->
            AgentOwnerDispatchOutcome.Rejected "Host reported admission without a durable prompt claim"

    let private interpretDispatchResult durable childId identitySeed prompt onAccepted =
        function
        | Ok _ -> classifySendSuccess durable childId identitySeed prompt onAccepted
        | Error error -> classifySendError durable childId identitySeed prompt onAccepted error

    let private sendAgentOwnerRootWithJournal
        (sessions: ISessionHostPort)
        (durable: AgentJournal)
        (childId: SessionId)
        (identitySeed: PromptAuthority.IdentitySeed)
        (directory: string option)
        (prompt: string)
        (onAccepted: ContinuationAcceptanceObserver)
        : Task<AgentOwnerDispatchOutcome> =
        task {
            let svc = PromptDispatcher.forPrompts (PromptJournalAdapter.create durable)

            let! sent =
                svc.SendAgentOwnerRoot
                    (DispatchSessionPort.ofSessionPort sessions)
                    childId
                    prompt
                    identitySeed
                    directory
                    PromptDispatcher.AwaitMode.Await
                    (Some onAccepted)

            return interpretDispatchResult durable childId identitySeed prompt onAccepted sent
        }

    let private chooseView delivered observed =
        match delivered with
        | Some record -> Some record
        | None -> observed

    /// Peek the delivered cell of this run without consuming it.
    ///
    /// The cell is fenced: only a delivery of this exact provider run is
    /// evidence for this terminal, and a render cache (no call fence) is
    /// presentation only. The peek advances no cursor and removes nothing,
    /// so repeated reads of one terminal yield the same view.
    let private peekDeliveredView (run: PendingHostRun) (result: AgentRunResult) (prose: string option) =
        match JsDeliveredValues.peekFence (SessionId.value run.ChildId) with
        | Some fence when fence.ProviderRun = result.ProviderRun ->
            JsDeliveredValues.tryPeekRenderedWith (SessionId.value run.ChildId) prose
        | _ -> None

    let private completedOutcomeView
        (childViewForRun: SessionId -> ProviderRunIdentity -> Task<string option>)
        (run: PendingHostRun)
        (result: AgentRunResult)
        =
        task {
            let! observed = childViewForRun run.ChildId result.ProviderRun

            let prose =
                if System.String.IsNullOrWhiteSpace result.TerminalText then
                    None
                else
                    Some result.TerminalText

            // D2: the cell is peeked, never taken. Two entries (the terminal
            // subscription and the runtime's Complete) read the same child
            // terminal; a consuming read would leave the second reader
            // without the material and strand the run.
            let delivered = peekDeliveredView run result prose

            return chooseView delivered observed
        }

    let viewForOutcome
        (childViewForRun: SessionId -> ProviderRunIdentity -> Task<string option>)
        (run: PendingHostRun)
        (outcome: TerminalOutcome)
        =
        match outcome with
        | TerminalOutcome.Completed result -> completedOutcomeView childViewForRun run result
        | _ -> Task.FromResult None

    /// The view of one terminal, resolved only when it is asked for.
    ///
    /// The read is deferred so that a terminal which does not address this
    /// run never touches the run's material. A faulting read is an empty
    /// view: it is bounded auxiliary evidence and never a reason to lose a
    /// terminal the child already proved.
    let safeViewResolution
        (childViewForRun: SessionId -> ProviderRunIdentity -> Task<string option>)
        (run: PendingHostRun)
        (outcome: TerminalOutcome)
        : ViewResolution =
        fun () ->
            task {
                try
                    return! viewForOutcome childViewForRun run outcome
                with error ->
                    logError "FORK-VIEW" (sprintf "run %s view read faulted: %s" run.AgentId error.Message)

                    return None
            }

    /// Idle existing child / first prompt for an AgentOwnerRoot work unit.
    ///
    /// PROMPT-005: the journal is required. A journal-less dispatcher would report
    /// success for a claim it never wrote, which is the one failure mode the
    /// four-fact protocol exists to prevent.
    let sendAgentOwnerRootObserved
        (sessions: ISessionHostPort)
        (journal: AgentJournal option)
        (childId: SessionId)
        (identitySeed: PromptAuthority.IdentitySeed)
        (directory: string option)
        (prompt: string)
        (onAccepted: ContinuationAcceptanceObserver)
        : Task<AgentOwnerDispatchOutcome> =
        match journal with
        | None ->
            Task.FromResult(AgentOwnerDispatchOutcome.Rejected "No journal: an AgentOwnerRoot prompt cannot be claimed")
        | Some durable ->
            sendAgentOwnerRootWithJournal sessions durable childId identitySeed directory prompt onAccepted

    /// PROMPT-006: every child prompt is an AgentOwnerRoot through the Dispatcher.
    ///
    /// The previous version fell back to `SendChildPromptFireAndForget` with
    /// `Metadata = None` whenever no journal was present. That path sent a real
    /// prompt with no PromptKey, so PROMPT-011 had no anchor to recover it by and
    /// PromptIngress could only classify the reply as UnknownOrigin.
    let sendChildPrompt
        (sessions: ISessionHostPort)
        (_parentId: SessionId)
        (journal: AgentJournal option)
        (childId: SessionId)
        (identitySeed: PromptAuthority.IdentitySeed)
        (directory: string option)
        (prompt: string)
        (onAccepted: ContinuationAcceptanceObserver)
        =
        sendAgentOwnerRootObserved sessions journal childId identitySeed directory prompt onAccepted

    let childPromptSender sessions parentId journal (directoryOf: string -> string option) =
        fun (agentId: string) childId (_role: Role) identitySeed prompt onAccepted ->
            sendChildPrompt sessions parentId journal childId identitySeed (directoryOf agentId) prompt onAccepted

    let private completionBelongsToRun (run: PendingHostRun) (result: AgentRunResult) =
        run.ChildId = result.SessionId
        && run.AuthorityRoot = result.AuthorityRootUserMessageId

    let private stopBelongsToRun (run: PendingHostRun) (stop: TerminalStop) =
        stop.AuthorityRootUserMessageId.IsNone
        || TerminalStop.belongsTo run.AuthorityRoot stop

    /// The synchronous sticky replay at install time must never settle a brand-new
    /// run with a predecessor's stored terminal. A replayed terminal is accepted
    /// only when it addresses this run: Completed needs the same child and
    /// authority root, Failed needs the exact authority root (no authority-less
    /// allowance — the live path keeps that allowance, the replay does not), and
    /// Aborted is observation only. Live delivery (after the run is armed) keeps
    /// the existing guards untouched.
    let private replayedTerminalAddressesRun (run: PendingHostRun) (outcome: TerminalOutcome) : bool =
        match outcome with
        | TerminalOutcome.Completed result -> completionBelongsToRun run result
        | TerminalOutcome.Failed stop -> TerminalStop.belongsTo run.AuthorityRoot stop
        | TerminalOutcome.Aborted _ -> false

    /// P0-RECOVERY-JOIN-001: only proven terminals may claim the cell.
    /// Aborted is observation — never recordCompletion / SetResult / mailbox.
    /// Claim runs only after JoinableCompletion proof succeeds (fail closed).
    let private claimPendingRun (gate: obj) (pendingRuns: Dictionary<string, PendingHostRun>) (run: PendingHostRun) =
        lock gate (fun () ->
            match pendingRuns.TryGetValue run.AgentId with
            | true, current when obj.ReferenceEquals(current.Token, run.Token) && not run.Finished ->
                run.Finished <- true
                true, run.Subscription
            | _ -> false, None)

    let private committedOutcome
        (journal: AgentJournal option)
        (parentId: SessionId)
        (proof: JoinableCompletion)
        (run: PendingHostRun)
        (agentOutcome: AgentCompletionOutcome)
        =
        task {
            let journalPort = journal |> Option.map AgentJournalPortAdapter.fromAgentJournal

            let commitment =
                match run.Work with
                | Some admitted -> HandleController.recordWorkCompletion journalPort parentId admitted proof
                | None -> Task.FromResult(Error "durable pending run has no admitted work proof")

            match! commitment with
            | Ok() -> return agentOutcome
            | Error error ->
                return
                    AgentCompletion.failed
                        run.AgentId
                        ("run-" + run.AgentId)
                        (Some run.Role)
                        (Some run.ChildId)
                        "PERSIST"
                        (sprintf "EXEC-009/PERSIST-002 HandleCompleted append failed: %s" error)
        }

    let private removeClaimedRun (gate: obj) (pendingRuns: Dictionary<string, PendingHostRun>) (run: PendingHostRun) =
        lock gate (fun () ->
            match pendingRuns.TryGetValue run.AgentId with
            | true, current when obj.ReferenceEquals(current.Token, run.Token) ->
                pendingRuns.Remove run.AgentId |> ignore
            | _ -> ())

    let private resolveFinalCommittedOutcome
        (journal: AgentJournal option)
        (parentId: SessionId)
        (proof: JoinableCompletion)
        (run: PendingHostRun)
        (agentOutcome: AgentCompletionOutcome)
        : Task<AgentCompletionOutcome> =
        match journal with
        | None -> Task.FromResult agentOutcome
        | Some j -> committedOutcome (Some j) parentId proof run agentOutcome

    let private isWorkCommittedOrRetired (work: HandleWork) =
        match work.Lifecycle with
        | CompletedAwaitingJoin _
        | Retired -> true
        | _ -> false

    let private isCompletionCommitted
        (journal: AgentJournal option)
        (parentId: SessionId)
        (run: PendingHostRun)
        : bool =
        journal
        |> Option.exists (fun durable ->
            run.Work
            |> Option.exists (fun admitted ->
                HandleProjection.tryWork (AdmittedWork.id admitted) (AgentJournal.handleProjection durable parentId)
                |> Option.exists isWorkCommittedOrRetired))

    let private releaseClaimedRunIfCommitted
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (run: PendingHostRun)
        =
        if journal.IsNone || isCompletionCommitted journal parentId run then
            removeClaimedRun gate pendingRuns run

    let private deliverClaimSettlementFault (run: PendingHostRun) (error: exn) =
        // The claim consumed this run's only completion promise. An
        // unsettled Source strands the Fork await and the join ledger
        // forever — settle an explicit failure even when settlement
        // itself faulted.
        let failedOutcome =
            AgentCompletion.failed
                run.AgentId
                ("run-" + run.AgentId)
                (Some run.Role)
                (Some run.ChildId)
                "PERSIST"
                (sprintf "completion settlement faulted: %s" error.Message)

        AsyncSupport.trySetResult run.Source failedOutcome |> ignore

    let private startClaimDelivery
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (run: PendingHostRun)
        (proof: JoinableCompletion)
        (agentOutcome: AgentCompletionOutcome)
        : Task =
        task {
            try
                let! finalOutcome = resolveFinalCommittedOutcome journal parentId proof run agentOutcome
                releaseClaimedRunIfCommitted gate pendingRuns journal parentId run
                run.Source.SetResult finalOutcome
            with error ->
                deliverClaimSettlementFault run error
        }
        :> Task

    let private disposeSubscriptionSafely (subscription: IDisposable option) =
        try
            subscription |> Option.iter (fun s -> s.Dispose())
        with _ ->
            ()

    let private deliverClaimedCompletion
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (gate: obj)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (run: PendingHostRun)
        (proof: JoinableCompletion)
        (agentOutcome: AgentCompletionOutcome)
        : Task =
        let claimed, subscriptionToDispose = claimPendingRun gate pendingRuns run

        if claimed then
            task {
                // The claim consumed this run's only completion promise. A faulting
                // subscription Dispose must not strand the Source: the delivery (and
                // its PERSIST failure settlement) always runs, even when cleanup
                // itself faults. The `do!` stays after the fault boundary, not in
                // a `finally` (a `finally` in `task { }` cannot await).
                disposeSubscriptionSafely subscriptionToDispose
                do! startClaimDelivery gate pendingRuns journal parentId run proof agentOutcome
            }
            :> Task
        else
            Task.FromResult(())

    let private deliverProvenCompletion
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (run: PendingHostRun)
        (evidence: TerminalEvidence)
        (agentOutcome: AgentCompletionOutcome)
        : Task =
        match JoinableCompletion.tryFromProvenTerminal evidence with
        | Error _ ->
            // Fail closed: leave run Active / pending for a later proven terminal.
            Task.FromResult(())
        | Ok proof -> deliverClaimedCompletion pendingRuns gate journal parentId run proof agentOutcome

    let private deliverFailedCompletion
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (run: PendingHostRun)
        (error: string)
        : Task =
        let childId = run.ChildId
        let runId = "run-" + run.AgentId
        let handle = HandleController.agentHandle run.AgentId
        let code = if error = "cancelled" then "CANCELLED" else "ERROR"

        let agentOutcome =
            AgentCompletion.failed run.AgentId runId (Some run.Role) (Some childId) code error

        let body = HandleCompletionCodec.encodeOutcome runId agentOutcome

        deliverProvenCompletion
            gate
            pendingRuns
            journal
            parentId
            run
            (TerminalEvidence.failed run.AgentId handle childId body)
            agentOutcome

    let complete
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
        (run: PendingHostRun)
        (outcome: TerminalOutcome)
        (view: ViewResolution option)
        : Task =
        let runId = "run-" + run.AgentId
        let childId = run.ChildId
        let handle = HandleController.agentHandle run.AgentId

        match outcome with
        | Aborted _ ->
            // Observation only. Keep pending run Active for a later proven terminal.
            Task.FromResult(())
        | Completed result when not result.IsValid ->
            // provider-attempt-recovery-008 / P0-RECOVERY-JOIN-001: an empty / XML-only terminal is
            // not a proven failure. The subagent auto-retries and continues — its
            // reconcile loop performs the bounded missing-final-report repair.
            // Concluding MISSING_FINAL_REPORT
            // here would fail the run before the last effort. Observation only.
            Task.FromResult(())
        | Completed result when not (completionBelongsToRun run result) -> Task.FromResult(())
        | Completed result ->
            task {
                // D6: a durable completion must be backed by a durable
                // delivery witness. Without a fence-matched witness nothing
                // proves that this run delivered anything, so the run stays
                // open (LC-004) instead of writing a HandleCompleted that
                // nobody witnessed. The journal-less harness path has no
                // witness to ask for and keeps working.
                let witnessed =
                    match journal with
                    | None -> true
                    | Some durable ->
                        // LogicalRunId 是实值：写入端与本处同源。
                        // 写入端取被调会话的 activeProfile.LogicalRunId
                        // （ToolRegistry.fs:821），而该 profile 就是子会话自己的
                        // ActiveLogicalRun（ProjectionQueries.fs:16-21，profileOwner
                        // 对非 fission lane 就是会话自己）；本处取子会话准入时记下的
                        // 逻辑运行（LinkageProjection.fs:310，准入用的也是该子会话
                        // 自己的 ActiveLogicalRun —— DelegationProjectionBridge.fs:100
                        // 取 childId 的 PromptAuthority）。同一条逻辑运行，
                        // 别的逻辑运行的见证不得满足本子运行的交付门禁。
                        let logicalRunId = run.Work |> Option.map AdmittedWork.logicalRunId

                        TaskDeliveryJournal.tryFindByFenceFields
                            durable
                            childId
                            result.ProviderRun
                            None
                            (Some result.AuthorityRootUserMessageId)
                            None
                            logicalRunId
                            // Task 不启用：写入端按「本会话的 lifecycle 退休 → 本会话
                            // 的子句柄 → 本次 toolCall」三级判定
                            // （ToolRegistry.fs:768-800），Manager 子运行的交付会落到
                            // Lifecycle 分支，其余才可能是 ChildWork 或 Invocation。
                            // 本处只能确定「这是哪个子运行」，断定不了写入端落到了哪一支；
                            // 断言 ChildWork 会让本该命中的见证永不匹配，D6 门禁下子运行
                            // 永远 witnessed=false 而卡死。缺失的这一位由交付见证的写入方
                            // 另行核对。
                            None
                        |> Option.isSome

                if not witnessed then
                    return ()
                else
                    // D1: the view is resolved only now. The terminal has
                    // been proven to address this run, so its material may
                    // be read and — once the outcome is admissible — spent.
                    let! resolved =
                        match view with
                        | Some resolution -> resolution ()
                        | None -> Task.FromResult None

                    match resolved with
                    | Some completedView when not (System.String.IsNullOrWhiteSpace completedView) ->
                        let prose =
                            if System.String.IsNullOrWhiteSpace result.TerminalText then
                                None
                            else
                                Some result.TerminalText

                        // D2: the delivered cell is consumed exactly once, and
                        // only for an admissible outcome. A take that finds
                        // nothing (another reader already consumed the cell)
                        // falls back to the peeked view, which is idempotent.
                        let taken =
                            JsDeliveredValues.tryTakeRenderedFor (SessionId.value run.ChildId) result.ProviderRun prose

                        let finalView =
                            match taken with
                            | Some consumed -> consumed
                            | None -> completedView

                        let agentOutcome =
                            AgentCompletion.completed
                                run.AgentId
                                childId
                                runId
                                run.Role
                                result.AuthorityRootUserMessageId
                                result.ProviderRun
                                finalView
                                result.Directory

                        let body = HandleCompletionCodec.encodeOutcome runId agentOutcome

                        do!
                            deliverProvenCompletion
                                gate
                                pendingRuns
                                journal
                                parentId
                                run
                                (TerminalEvidence.completed run.AgentId handle childId body)
                                agentOutcome
                    | _ ->
                        // LC-004: An asynchronous child that completed its physical turn without
                        // delivering a view has not delivered its obligation. Do not settle or
                        // close the run; observation only, the child continues.
                        return ()
            }
            :> Task
        | Failed stop when
            stop.Reason = "MISSING_FINAL_REPORT"
            || stop.Reason.Contains("MISSING_FINAL_REPORT")
            ->
            // provider-attempt-recovery-008 / P0-RECOVERY-JOIN-001: a missing final report is not a
            // proven terminal failure. The subagent auto-retries and continues (its
            // reconcile loop keeps repairing the empty terminal); delivering a
            // proven MISSING_FINAL_REPORT failure here concludes the run before the
            // last effort (the same reason the `Aborted` branch observes only).
            // Observation only — keep pending run Active for a later proven terminal.
            Task.FromResult(())
        | Failed stop when not (stopBelongsToRun run stop) ->
            // WHAT[delegation-025] / [031]: a rooted stop only settles the work
            // unit whose own admitted Authority Root it addresses. A stop that
            // carries another (older) work unit's root is a foreign terminal:
            // it must not claim this run's completion cell and must not write
            // this run's completion evidence. An authority-less session-wide
            // stop still addresses the run (the live-path allowance).
            Task.FromResult(())
        | Failed stop -> deliverFailedCompletion gate pendingRuns journal parentId run stop.Reason

    let private recoverFailedSettlement
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (run: PendingHostRun)
        (error: exn)
        : Task =
        task {
            logError "FORK-SETTLEMENT" (sprintf "run %s completion settlement faulted: %s" run.AgentId error.Message)

            try
                do!
                    deliverFailedCompletion
                        gate
                        pendingRuns
                        journal
                        parentId
                        run
                        (sprintf "completion settlement faulted: %s" error.Message)
            with recovery ->
                logError "FORK-SETTLEMENT" (sprintf "run %s failure delivery faulted: %s" run.AgentId recovery.Message)

                AsyncSupport.trySetResult
                    run.Source
                    (AgentCompletion.failed
                        run.AgentId
                        ("run-" + run.AgentId)
                        (Some run.Role)
                        (Some run.ChildId)
                        "PERSIST"
                        (sprintf "completion settlement faulted: %s" error.Message))
                |> ignore
        }

    let installRun
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
        (childViewForRun: SessionId -> ProviderRunIdentity -> Task<string option>)
        (trackOwnedWork: (unit -> Task) -> unit)
        (agentId: string)
        (childId: SessionId)
        (role: Role)
        (authorityRoot: AuthorityRootUserMessageId)
        =
        JsDeliveredValues.clear (SessionId.value childId)

        let work =
            match journal with
            | None -> invalidOp "a pending agent work requires a canonical journal"
            | Some durable ->
                admittedWork durable parentId agentId childId authorityRoot
                |> Result.defaultWith (fun reason -> invalidOp (sprintf "exact work admission missing: %A" reason))

        let run =
            { Token = obj ()
              AgentId = agentId
              ChildId = childId
              Role = role
              AuthorityRoot = authorityRoot
              Work = Some work
              Source = HostPendingRun.completionSource ()
              Subscription = None
              Finished = false }

        lock gate (fun () -> pendingRuns.[agentId] <- run)

        // DSL-MUTABLE: algorithm-scratch — install-time replay window flag
        let mutable armed = false

        let deliverTerminal (outcome: TerminalOutcome) =
            trackOwnedWork (fun () ->
                task {
                    // D1: the view is not read when the terminal arrives.
                    // The resolution is handed to the lifecycle owner, which
                    // reads it only after the terminal has been proven to
                    // address this run — a foreign or replayed terminal must
                    // not spend the live run's material. EXEC-006: a view
                    // read is bounded auxiliary evidence, so its failure is
                    // an empty view, never a reason to lose a proven
                    // terminal that already happened on the child.
                    let view = Some(safeViewResolution childViewForRun run outcome)

                    try
                        do! complete gate pendingRuns journal parentId sessions run outcome view
                    with error ->
                        // A terminal the child proved must still resolve
                        // this run's only completion promise even when
                        // settlement faulted: settle an explicit joinable
                        // failure (never silence, never a stranded Source).
                        do! recoverFailedSettlement gate pendingRuns journal parentId run error
                }
                :> Task)

        let subscription =
            // Sticky subscription: a terminal that fired between prompt acceptance
            // and run installation (e.g. a pre-provider admission failure settling
            // synchronously inside chat.message) must be replayed here, or the run
            // is stranded and the parent's join never settles. Until the run is
            // armed, every delivery is a replay (or a same-tick race) and must
            // address this run — a predecessor's stored terminal must not settle a
            // brand-new run. Once armed, the existing guards own the live path.
            sessions.SubscribeTerminal(
                childId,
                (fun _ outcome ->
                    if not armed && not (replayedTerminalAddressesRun run outcome) then
                        ()
                    else
                        deliverTerminal outcome)
            )

        let disposeImmediately =
            lock gate (fun () ->
                run.Subscription <- Some subscription
                armed <- true
                run.Finished)

        if disposeImmediately then
            subscription.Dispose()

        run

    /// Parent cancellation has already committed durable HandleAbandoned before
    /// this runs. Settle only the in-memory waiter/subscription; routing the same
    /// run through `complete(Failed "cancelled")` would incorrectly compete with
    /// Abandoned by attempting a HandleCompleted(CANCELLED) commit.
    let settleParentCancelled (gate: obj) (pendingRuns: Dictionary<string, PendingHostRun>) (run: PendingHostRun) =
        let claimed, subscriptionToDispose =
            lock gate (fun () ->
                match pendingRuns.TryGetValue run.AgentId with
                | true, current when obj.ReferenceEquals(current.Token, run.Token) && not run.Finished ->
                    run.Finished <- true
                    pendingRuns.Remove run.AgentId |> ignore
                    true, run.Subscription
                | _ -> false, None)

        if claimed then
            subscriptionToDispose
            |> Option.iter (fun subscription -> subscription.Dispose())

            run.Source.SetResult(AgentCompletion.abandoned run.AgentId "ParentCancelled")

    let failRun
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
        (run: PendingHostRun)
        (error: string)
        =
        deliverFailedCompletion gate pendingRuns journal parentId run error

    /// Terminal outcomes are always accepted by complete. Call sites keep
    /// MarkReady for API shape; body is intentionally a no-op.
    let markReady
        (_gate: obj)
        (_pendingRuns: Dictionary<string, PendingHostRun>)
        (_journal: AgentJournal option)
        (_parentId: SessionId)
        (_sessions: ISessionHostPort)
        (_run: PendingHostRun)
        (_view: string option)
        =
        ()
