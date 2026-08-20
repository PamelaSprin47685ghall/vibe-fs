namespace Wanxiangshu.Execution.Delegation.Fork.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
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
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Mission.Obligation.Todo

/// Per-run terminal lifecycle for HostForkRuntime: install, complete, fail.
module HostForkRunLifecycle =

    [<RequireQualifiedAccess>]
    type AgentOwnerDispatchOutcome =
        | Accepted
        | AcceptanceUncertain of string
        | Rejected of string

    let private durableDispatchObservation
        (durable: AgentJournal)
        (childId: SessionId)
        (payloadDigest: string)
        =
        let projections = (AgentJournal.snapshot durable).AgentProjections

        match PromptAuthorityLedger.dispatchStatusFor childId payloadDigest projections with
        | PromptAuthorityLedger.DispatchStatus.Accepted evidence -> Choice1Of3 evidence
        | PromptAuthorityLedger.DispatchStatus.Pending ->
            match PromptAuthorityLedger.pendingDispatchClaim childId payloadDigest projections with
            | Some claim -> Choice2Of3 claim
            | None ->
                raise (
                    InvalidOperationException(
                        "PromptAuthority projection reported Pending without the matching durable claim"
                    )
                )
        | PromptAuthorityLedger.DispatchStatus.Dispatchable -> Choice3Of3()

    let private classifySendError
        (durable: AgentJournal)
        (childId: SessionId)
        (prompt: string)
        (onAccepted: PhysicalUserMessageId -> unit)
        (error: string)
        =
        let payloadDigest = HostDigest.sha256Hex prompt

        let accepted evidence =
            // Close the race where PhysicalAccepted landed after the dispatcher
            // cancelled its synchronous waiter but before we inspected durable
            // evidence. Binding the same root twice is idempotent assignment.
            onAccepted evidence.PhysicalUserMessageId
            AgentOwnerDispatchOutcome.Accepted

        match durableDispatchObservation durable childId payloadDigest with
        | Choice1Of3 evidence -> accepted evidence
        | Choice3Of3() -> AgentOwnerDispatchOutcome.Rejected error
        | Choice2Of3 claim ->
            // AcceptanceUnknown intentionally leaves the claim Pending. Restore
            // the process-local callback that PromptDispatcher cancelled when its
            // synchronous Result was Error, then re-read durable truth to close
            // the accepted-between-read-and-register race.
            PromptPhysicalAcceptance.register claim.PromptKey onAccepted

            match durableDispatchObservation durable childId payloadDigest with
            | Choice1Of3 evidence ->
                PromptPhysicalAcceptance.cancel claim.PromptKey
                accepted evidence
            | Choice2Of3 _ -> AgentOwnerDispatchOutcome.AcceptanceUncertain error
            | Choice3Of3() ->
                PromptPhysicalAcceptance.cancel claim.PromptKey
                AgentOwnerDispatchOutcome.Rejected error

    let workRecordForOutcome
        (childWorkRecordForRun: SessionId -> MagicTodoLwr.BoundedRange -> ProviderRunIdentity -> Task<string option>)
        (xTraceHead: SessionId -> int64)
        (run: PendingHostRun)
        (outcome: TerminalOutcome)
        =
        match outcome with
        | TerminalOutcome.Completed result ->
            childWorkRecordForRun
                run.ChildId
                (DelegationHandoff.childRange run.StartCursor (xTraceHead run.ChildId))
                result.ProviderRun
        | _ -> Task.FromResult None

    /// Idle existing child / first prompt for an AgentOwnerRoot work unit.
    ///
    /// PROMPT-005: the journal is required. A journal-less dispatcher would report
    /// success for a claim it never wrote, which is the one failure mode the
    /// four-fact protocol exists to prevent.
    let sendAgentOwnerRootObserved
        (sessions: ISessionHostPort)
        (journal: AgentJournal option)
        (childId: SessionId)
        (agent: string)
        (directory: string option)
        (prompt: string)
        (onAccepted: PhysicalUserMessageId -> unit)
        : Task<AgentOwnerDispatchOutcome> =
        task {
            match journal with
            | None -> return AgentOwnerDispatchOutcome.Rejected "No journal: an AgentOwnerRoot prompt cannot be claimed"
            | Some durable ->
                let svc = PromptDispatcher.forJournal durable
                let! sent =
                    svc.SendAgentOwnerRoot
                        sessions
                        childId
                        prompt
                        agent
                        directory
                        PromptDispatcher.AwaitMode.Await
                        (Some onAccepted)

                return
                    match sent with
                    | Ok _ -> AgentOwnerDispatchOutcome.Accepted
                    | Error error -> classifySendError durable childId prompt onAccepted error
        }

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
        (agent: string)
        (directory: string option)
        (prompt: string)
        (onAccepted: PhysicalUserMessageId -> unit)
        =
        sendAgentOwnerRootObserved sessions journal childId agent directory prompt onAccepted

    let childPromptSender sessions parentId journal directoryOf =
        fun agentId childId (_role: Role) agent prompt onAccepted ->
            sendChildPrompt sessions parentId journal childId agent (directoryOf agentId) prompt onAccepted

    let bindAuthorityRoot (run: PendingHostRun) (physical: PhysicalUserMessageId) =
        run.AuthorityRoot <- Some(PhysicalUserMessageId.promoteToAuthorityRoot physical)

    let private completionBelongsToRun (run: PendingHostRun) (result: AgentRunResult) =
        match run.Handoff with
        | None -> true
        | Some _ -> run.AuthorityRoot = Some result.AuthorityRootUserMessageId

    let private stopBelongsToRun (run: PendingHostRun) (stop: TerminalStop) =
        match run.Handoff with
        | None -> true
        | Some _ ->
            run.AuthorityRoot
            |> Option.exists (fun root -> TerminalStop.belongsTo root stop)

    let private checkpointCompletedOrCrash
        (handoffPort: ReusableHandoffPort option)
        (parentId: SessionId)
        (run: PendingHostRun)
        : Task =
        let fail detail =
            FatalProcess.trip "HostForkRunLifecycle.checkpointCompletedHandoff" detail
            raise (InvalidOperationException detail)

        let requireCheckpoint =
            function
            | Ok() -> ()
            | Error error -> fail (sprintf "delegation completed-handoff append failed: %s" error)

        let checkpoint =
            match run.Handoff, handoffPort with
            | None, _ -> None
            | Some handoff, Some port -> Some(port, handoff)
            | Some _, None -> fail "reusable fork run has no handoff capability"

        match checkpoint with
        | None -> Task.FromResult(()) :> Task
        | Some(port, handoff) ->
            task {
                let! result = port.CheckpointCompleted parentId handoff
                return requireCheckpoint result
            }
            :> Task

    /// P0-RECOVERY-JOIN-001: only proven terminals may claim the cell.
    /// Aborted is observation — never recordCompletion / SetResult / mailbox.
    /// Claim runs only after JoinableCompletion proof succeeds (fail closed).
    let private claimPendingRun (gate: obj) (pendingRuns: Dictionary<string, PendingHostRun>) (run: PendingHostRun) =
        lock gate (fun () ->
            match pendingRuns.TryGetValue run.AgentId with
            | true, current when obj.ReferenceEquals(current.Token, run.Token) && not run.Finished ->
                run.Finished <- true
                pendingRuns.Remove run.AgentId |> ignore
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
            match! ChildRecoveryWorkflow.commitJoinable journal parentId proof with
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

    let private startClaimDelivery
        (journal: AgentJournal option)
        (parentId: SessionId)
        (run: PendingHostRun)
        (proof: JoinableCompletion)
        (agentOutcome: AgentCompletionOutcome)
        : Task =
        task {
            let! finalOutcome =
                match journal with
                | None -> Task.FromResult agentOutcome
                | Some j -> committedOutcome (Some j) parentId proof run agentOutcome

            run.Source.SetResult finalOutcome
        }
        :> Task

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
            subscriptionToDispose
            |> Option.iter (fun subscription -> subscription.Dispose())

            startClaimDelivery journal parentId run proof agentOutcome
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
        (handoffPort: ReusableHandoffPort option)
        (run: PendingHostRun)
        (outcome: TerminalOutcome)
        (workRecord: string option)
        : Task =
        // EXEC-006 / COMPANION-003: the completion's work record is the child's
        // final LifecycleWorkRecord only — Opening + frames + gap + terminal,
        // materialised by the same port as parent background. No TerminalText
        // or session-A fallback: a missing LWR is an empty work_record, not a
        // second channel.
        let completedWorkRecord = workRecord
        let runId = "run-" + run.AgentId
        let childId = run.ChildId
        let handle = HandleController.agentHandle run.AgentId

        match outcome with
        | Aborted _ ->
            // Observation only. Keep pending run Active for a later proven terminal.
            Task.FromResult(())
        | Completed result when not result.IsValid ->
            // FALLBACK-008 / P0-RECOVERY-JOIN-001: an empty / XML-only terminal is
            // not a proven failure. The subagent auto-retries and continues — its
            // reconcile loop repairs the missing final report (RepairOnce /
            // AbandonRoundProduct, never FailSlot). Concluding MISSING_FINAL_REPORT
            // here would fail the run before the last effort. Observation only.
            Task.FromResult(())
        | Completed result when not (completionBelongsToRun run result) -> Task.FromResult(())
        | Completed result ->
            task {
                do! checkpointCompletedOrCrash handoffPort parentId run

                let agentOutcome =
                    AgentCompletion.completed
                        run.AgentId
                        childId
                        runId
                        run.Role
                        result.AuthorityRootUserMessageId
                        result.ProviderRun
                        (completedWorkRecord |> Option.defaultValue "")
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
            }
            :> Task
        | Failed stop when not (stopBelongsToRun run stop) -> Task.FromResult(())
        | Failed stop when stop.Reason = "MISSING_FINAL_REPORT" || stop.Reason.Contains("MISSING_FINAL_REPORT") ->
            // FALLBACK-008 / P0-RECOVERY-JOIN-001: a missing final report is not a
            // proven terminal failure. The subagent auto-retries and continues (its
            // reconcile loop keeps repairing the empty terminal); delivering a
            // proven MISSING_FINAL_REPORT failure here concludes the run before the
            // last effort (the same reason the `Aborted` branch observes only).
            // Observation only — keep pending run Active for a later proven terminal.
            Task.FromResult(())
        | Failed stop -> deliverFailedCompletion gate pendingRuns journal parentId run stop.Reason

    let installRun
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
        (childWorkRecordForRun: SessionId -> MagicTodoLwr.BoundedRange -> ProviderRunIdentity -> Task<string option>)
        (xTraceHead: SessionId -> int64)
        (trackOwnedWork: (unit -> Task) -> unit)
        (handoffPort: ReusableHandoffPort option)
        (handoff: PreparedDelegationHandoff option)
        (agentId: string)
        (childId: SessionId)
        (role: Role)
        =
        let run =
            { Token = obj ()
              AgentId = agentId
              ChildId = childId
              Role = role
              StartCursor = xTraceHead childId
              Handoff = handoff
              AuthorityRoot = None
              Source = HostPendingRun.completionSource ()
              Subscription = None
              Finished = false }

        lock gate (fun () -> pendingRuns.[agentId] <- run)

        let subscription =
            sessions.SubscribeFutureTerminal(
                childId,
                (fun _ outcome ->
                    trackOwnedWork (fun () ->
                        task {
                            let! workRecord = workRecordForOutcome childWorkRecordForRun xTraceHead run outcome
                            do! complete gate pendingRuns journal parentId sessions handoffPort run outcome workRecord
                        }
                        :> Task))
            )

        let disposeImmediately =
            lock gate (fun () ->
                run.Subscription <- Some subscription
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
        (handoffPort: ReusableHandoffPort option)
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
        (_workRecord: string option)
        =
        ()
