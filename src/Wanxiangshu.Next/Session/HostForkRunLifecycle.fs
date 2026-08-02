namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

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
                let! sent = svc.SendAgentOwnerRoot sessions childId prompt agent directory None
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
        let suppliedWorkRecord = workRecord

        // EXEC-008 / COMPANION-003: the completion's work record is the child's
        // final LifecycleWorkRecord — opening, frames, gap, terminal. When the
        // durable record is unavailable, the terminal text stands in: the same
        // self-contained value, not a parallel final-text channel.
        let completedWorkRecord (result: AgentRunResult) =
            suppliedWorkRecord
            |> Option.orElseWith (fun () ->
                if String.IsNullOrWhiteSpace result.TerminalText then
                    None
                else
                    Some result.TerminalText)

        // Only the first matching terminal may claim the run. Duplicate idle/
        // abort from dual event streams must not SetResult twice.
        let claimed, subscriptionToDispose =
            lock gate (fun () ->
                match pendingRuns.TryGetValue run.AgentId with
                | true, current when obj.ReferenceEquals(current.Token, run.Token) && run.Ready && not run.Finished ->
                    run.Finished <- true
                    pendingRuns.Remove run.AgentId |> ignore
                    true, run.Subscription
                | _ -> false, None)

        if claimed then
            subscriptionToDispose
            |> Option.iter (fun subscription -> subscription.Dispose())

            // EXEC-009: the durable completion precedes the mailbox delivery. The
            // `join` that consumes the completion retires the handle, and the fold
            // rejects that retirement unless `HandleCompleted` is already on disk —
            // measured on Host 1.18.10: every agent `join` poisoned the journal
            // with `join retired a handle that had no completion (EXEC-004)`, which
            // permanently disabled every later durable effect. The previous code
            // wrote the completion only on the cancel path.
            let completionKind =
                match outcome with
                | Completed _ -> HandleCompletionKind.Terminal
                | Aborted _
                | Failed _ -> HandleCompletionKind.SendFailure

            match journal with
            | None -> ()
            | Some _ ->
                match HandleController.recordCompletion journal parentId run.AgentId completionKind with
                | Ok() -> ()
                | Error error -> failwith (sprintf "EXEC-009/PERSIST-002 HandleCompleted append failed: %s" error)

            let runId = "run-" + run.AgentId
            let childId = run.ChildId

            match outcome with
            | Completed result ->
                // EXEC-006: `IsValid` is the one place that decides whether a
                // completed run actually carries session-wide A. Re-testing the
                // text here would be a second copy of that rule.
                if not result.IsValid then
                    run.Source.SetResult(
                        AgentCompletion.failed
                            run.AgentId
                            runId
                            (Some run.Role)
                            (Some childId)
                            "MISSING_FINAL_REPORT"
                            "completed with empty session-wide text"
                    )
                else
                    run.Source.SetResult(
                        AgentCompletion.completed
                            run.AgentId
                            childId
                            runId
                            run.Role
                            result.AuthorityRootUserMessageId
                            // HOST-010/HOST-011: the terminal provider run IS the
                            // assistant message, so there is no separate id to pass.
                            result.ProviderRun
                            (completedWorkRecord result |> Option.defaultValue "")
                            result.Directory
                    )
            | Aborted reason ->
                run.Source.SetResult(
                    AgentCompletion.aborted run.AgentId runId (Some run.Role) (Some childId) "ABORTED" reason
                )
            | Failed error ->
                let code =
                    if error = "MISSING_FINAL_REPORT" || error.Contains("MISSING_FINAL_REPORT") then
                        "MISSING_FINAL_REPORT"
                    elif error = "cancelled" then
                        "CANCELLED"
                    else
                        "ERROR"

                run.Source.SetResult(AgentCompletion.failed run.AgentId runId (Some run.Role) (Some childId) code error)

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
              Ready = false
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
        lock gate (fun () -> run.Ready <- true)
        complete gate pendingRuns journal parentId sessions run (TerminalOutcome.Failed error) None

    let markReady (gate: obj) (run: PendingHostRun) = lock gate (fun () -> run.Ready <- true)
