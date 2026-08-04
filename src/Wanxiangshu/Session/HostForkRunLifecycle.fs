namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Journal

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
        fun agentId childId (_role: AgentRole) agent prompt ->
            sendChildPrompt sessions parentId journal childId agent (directoryOf agentId) prompt

    let private deliverCompletion
        (journal: AgentJournal option)
        (parentId: SessionId)
        (run: PendingHostRun)
        (outcome: TerminalOutcome)
        (workRecord: string option)
        (subscriptionToDispose: IDisposable option)
        =
        subscriptionToDispose
        |> Option.iter (fun subscription -> subscription.Dispose())

        // EXEC-009: durable blob + HandleCompleted precede mailbox delivery.
        // Join consumes from projection/blob; mailbox is wait-notification only.
        let runId = "run-" + run.AgentId
        let childId = run.ChildId

        let completionKind, agentOutcome =
            match outcome with
            | Completed result when not result.IsValid ->
                // EXEC-006: `IsValid` decides whether a completed run carries
                // terminal output. The join wire then carries the materialised LWR.
                HandleCompletionKind.SendFailure,
                AgentCompletion.failed
                    run.AgentId
                    runId
                    (Some run.Role)
                    (Some childId)
                    "MISSING_FINAL_REPORT"
                    "completed with empty terminal output"
            | Completed result ->
                HandleCompletionKind.Terminal,
                AgentCompletion.completed
                    run.AgentId
                    childId
                    runId
                    run.Role
                    result.AuthorityRootUserMessageId
                    // HOST-010/HOST-011: terminal provider run IS the assistant message.
                    result.ProviderRun
                    (workRecord |> Option.defaultValue "")
                    result.Directory
            | Aborted reason ->
                HandleCompletionKind.SendFailure,
                AgentCompletion.aborted run.AgentId runId (Some run.Role) (Some childId) "ABORTED" reason
            | Failed error ->
                let code =
                    if error = "MISSING_FINAL_REPORT" || error.Contains("MISSING_FINAL_REPORT") then
                        "MISSING_FINAL_REPORT"
                    elif error = "cancelled" then
                        "CANCELLED"
                    else
                        "ERROR"

                HandleCompletionKind.SendFailure,
                AgentCompletion.failed run.AgentId runId (Some run.Role) (Some childId) code error

        let body = Some(HandleCompletionCodec.encodeOutcome runId agentOutcome)

        let finalOutcome =
            match journal with
            | None -> agentOutcome
            | Some _ ->
                match HandleController.recordCompletion journal parentId run.AgentId completionKind body with
                | Ok() -> agentOutcome
                | Error error ->
                    // Never leave Source unset: Join must unblock even when append fails.
                    AgentCompletion.failed
                        run.AgentId
                        runId
                        (Some run.Role)
                        (Some childId)
                        "PERSIST"
                        (sprintf "EXEC-009/PERSIST-002 HandleCompleted append failed: %s" error)

        run.Source.SetResult finalOutcome

    let complete
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
        (run: PendingHostRun)
        (outcome: TerminalOutcome)
        (workRecord: string option)
        =
        // EXEC-006 / COMPANION-003: the completion's work record is the child's
        // final LifecycleWorkRecord only — Opening + frames + gap + terminal,
        // materialised by the same port as parent background. No TerminalText
        // or session-A fallback: a missing LWR is an empty work_record, not a
        // second channel.
        let completedWorkRecord = workRecord

        // First matching terminal claims the run immediately. No Ready gate:
        // install/recovery/restart windows must not buffer-and-lose completion.
        // Token + Finished prevent stale or double SetResult.
        let claimed, subscriptionToDispose =
            lock gate (fun () ->
                match pendingRuns.TryGetValue run.AgentId with
                | true, current when obj.ReferenceEquals(current.Token, run.Token) && not run.Finished ->
                    run.Finished <- true
                    pendingRuns.Remove run.AgentId |> ignore
                    true, run.Subscription
                | _ -> false, None)

        if claimed then
            deliverCompletion journal parentId run outcome completedWorkRecord subscriptionToDispose

    let installRun
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (sessions: ISessionHostPort)
        (childWorkRecordFor: SessionId -> string option)
        (agentId: string)
        (childId: SessionId)
        (role: AgentRole)
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
            | _ -> None

        let subscription =
            sessions.SubscribeTerminal(
                childId,
                (fun _ outcome ->
                    complete gate pendingRuns journal parentId sessions run outcome (terminalWorkRecord outcome))
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
