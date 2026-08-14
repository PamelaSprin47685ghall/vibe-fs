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

/// Per-run terminal lifecycle for HostForkRuntime: install, complete, fail.
module HostForkRunLifecycle =

    /// Idle existing child / first prompt for an AgentOwnerRoot work unit.
    ///
    /// PROMPT-005: the journal is required. A journal-less dispatcher would report
    /// success for a claim it never wrote, which is the one failure mode the
    /// four-fact protocol exists to prevent.
    let sendAgentOwnerRoot
        (sessions: ISessionHostPort)
        (journal: AgentJournal option)
        (childId: SessionId)
        (agent: string)
        (directory: string option)
        (prompt: string)
        : Task<Result<unit, string>> =
        task {
            match journal with
            | None -> return Error "No journal: an AgentOwnerRoot prompt cannot be claimed"
            | Some durable ->
                let svc = PromptDispatcher.forJournal durable
                // PROMPT-007 Detached: child dispatch does not wait for PhysicalAccepted.
                let! sent =
                    svc.SendAgentOwnerRoot
                        sessions
                        childId
                        prompt
                        agent
                        directory
                        PromptDispatcher.AwaitMode.Detached
                        None

                return sent |> Result.map ignore
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
        =
        sendAgentOwnerRoot sessions journal childId agent directory prompt

    let childPromptSender sessions parentId journal directoryOf =
        fun agentId childId (_role: Role) agent prompt ->
            sendChildPrompt sessions parentId journal childId agent (directoryOf agentId) prompt

    /// P0-RECOVERY-JOIN-001: only proven terminals may claim the cell.
    /// Aborted is observation — never recordCompletion / SetResult / mailbox.
    /// Claim runs only after JoinableCompletion proof succeeds (fail closed).
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
        | Ok proof ->
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

                // EXEC-009: durable blob + HandleCompleted is the agent fact source (GREEN-5).
                // Join re-reads Journal; mailbox is wake-only for agents.
                // P0 §十: sole production owner of recordCompletion is ChildRecoveryWorkflow.
                let runId = "run-" + run.AgentId
                let childId = run.ChildId

                // Terminal callbacks are synchronous Host notifications, so the
                // durable claim continues as an explicit detached Task. The Task
                // itself preserves the ordering: SetResult happens only after the
                // HandleCompleted commit has resolved.
                task {
                    let! finalOutcome =
                        match journal with
                        | None -> Task.FromResult agentOutcome
                        | Some _ ->
                            task {
                                match! ChildRecoveryWorkflow.commitJoinable journal parentId proof with
                                | Ok() -> return agentOutcome
                                | Error error ->
                                    return
                                        AgentCompletion.failed
                                            run.AgentId
                                            runId
                                            (Some run.Role)
                                            (Some childId)
                                            "PERSIST"
                                            (sprintf "EXEC-009/PERSIST-002 HandleCompleted append failed: %s" error)
                            }

                    run.Source.SetResult finalOutcome
                }
                :> Task
            else
                Task.FromResult(())

    let complete
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
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
        | Completed result ->
            let agentOutcome =
                AgentCompletion.completed
                    run.AgentId
                    childId
                    runId
                    run.Role
                    result.AuthorityRootUserMessageId
                    // HOST-010/HOST-011: terminal provider run IS the assistant message.
                    result.ProviderRun
                    (completedWorkRecord |> Option.defaultValue "")
                    result.Directory

            let body = HandleCompletionCodec.encodeOutcome runId agentOutcome

            deliverProvenCompletion
                gate
                pendingRuns
                journal
                parentId
                run
                (TerminalEvidence.completed run.AgentId handle childId body)
                agentOutcome
        | Failed error when error = "MISSING_FINAL_REPORT" || error.Contains("MISSING_FINAL_REPORT") ->
            // FALLBACK-008 / P0-RECOVERY-JOIN-001: a missing final report is not a
            // proven terminal failure. The subagent auto-retries and continues (its
            // reconcile loop keeps repairing the empty terminal); delivering a
            // proven MISSING_FINAL_REPORT failure here concludes the run before the
            // last effort (the same reason the `Aborted` branch observes only).
            // Observation only — keep pending run Active for a later proven terminal.
            Task.FromResult(())
        | Failed error ->
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

    let installRun
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
        (childWorkRecordFor: SessionId -> Task<string option>)
        (agentId: string)
        (childId: SessionId)
        (role: Role)
        =
        let run =
            { Token = obj ()
              AgentId = agentId
              ChildId = childId
              Role = role
              Source = HostPendingRun.completionSource ()
              Subscription = None
              Finished = false }

        lock gate (fun () -> pendingRuns.[agentId] <- run)

        let terminalWorkRecord outcome =
            match outcome with
            | Completed _ -> childWorkRecordFor childId
            | _ -> Task.FromResult None

        let subscription =
            sessions.SubscribeTerminal(
                childId,
                (fun _ outcome ->
                    task {
                        let! workRecord = terminalWorkRecord outcome
                        do! complete gate pendingRuns journal parentId sessions run outcome workRecord
                    }
                    |> ignore)
            )

        let disposeImmediately =
            lock gate (fun () ->
                run.Subscription <- Some subscription
                run.Finished)

        if disposeImmediately then
            subscription.Dispose()

        run

    let failRun
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
        (run: PendingHostRun)
        (error: string)
        =
        complete gate pendingRuns journal parentId sessions run (TerminalOutcome.Failed error) None

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
