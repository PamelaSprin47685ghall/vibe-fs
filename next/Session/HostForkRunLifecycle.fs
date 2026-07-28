namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

/// Per-run terminal lifecycle for HostForkRuntime: install, complete, fail.
module HostForkRunLifecycle =

    let private authorityService (journal: AgentJournal option) =
        match journal with
        | Some j -> PromptDispatcher.forJournal j
        | None -> PromptDispatcher.ephemeral ()

    /// Idle existing child / first prompt for an AgentOwnerRoot work unit.
    let sendAgentOwnerRoot
        (sessions: ISessionHostPort)
        (journal: AgentJournal option)
        (childId: SessionId)
        (agent: string)
        (directory: string option)
        (prompt: string)
        : Task<Result<unit, string>> =
        task {
            let svc = authorityService journal

            let! sent = svc.SendAgentOwnerRoot sessions childId prompt agent directory None

            match sent with
            | Ok _ -> return Ok()
            | Error err -> return Error err
        }

    let sendChildPrompt
        (sessions: ISessionHostPort)
        (parentId: SessionId)
        (journal: AgentJournal option)
        (childId: SessionId)
        (agent: string)
        (directory: string option)
        (prompt: string)
        =
        // Prefer two-phase AgentOwnerRoot. Fallback only when no journal is present.
        match journal with
        | Some _ -> sendAgentOwnerRoot sessions journal childId agent directory prompt
        | None ->
            sessions.SendChildPromptFireAndForget(
                parentId,
                childId,
                prompt,
                { Model = None
                  Agent = Some agent
                  Directory = directory
                  Metadata = None }
            )

    let childPromptSender sessions parentId journal directoryOf =
        fun agentId childId (_role: AgentRole) agent prompt ->
            sendChildPrompt sessions parentId journal childId agent (directoryOf agentId) prompt

    let complete
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (sessions: ISessionHostPort)
        (run: PendingHostRun)
        (outcome: TerminalOutcome)
        (workRecord: WorkRecordSnapshot option)
        =
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

            let runId = "run-" + run.AgentId
            let childId = SessionId.value run.ChildId

            match outcome with
            | Completed result ->
                if String.IsNullOrWhiteSpace result.FinalText then
                    run.Source.SetResult(
                        AgentCompletion.failed
                            run.AgentId
                            runId
                            (Some run.Role)
                            (Some childId)
                            "MISSING_FINAL_REPORT"
                            "completed with empty final text"
                    )
                else
                    run.Source.SetResult(
                        AgentCompletion.completed
                            run.AgentId
                            childId
                            runId
                            run.Role
                            (MessageId.value result.RootUserMessageId)
                            (MessageId.value result.AssistantMessageId)
                            result.FinalText
                            workRecord
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
        (sessions: ISessionHostPort)
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

        let subscription =
            sessions.SubscribeTerminal(childId, (fun _ outcome -> complete gate pendingRuns sessions run outcome None))

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
        (sessions: ISessionHostPort)
        (run: PendingHostRun)
        (error: string)
        =
        lock gate (fun () -> run.Ready <- true)
        complete gate pendingRuns sessions run (TerminalOutcome.Failed error) None

    let markReady (gate: obj) (run: PendingHostRun) = lock gate (fun () -> run.Ready <- true)
