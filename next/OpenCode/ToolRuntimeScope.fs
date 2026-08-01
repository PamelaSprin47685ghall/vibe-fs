namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

/// Owns every per-session tool runtime.
///
/// AGENT-007: Role comes from the Authority Root's CanonicalRole and nothing
/// else. The previous version consulted three sources in order — authority, a
/// per-session role cache, then the Host context's `agent` field — so a session
/// whose authority said Coder could still be gated as DevOps because a cache
/// entry or a message field said so. Two of the three are gone.
type ToolRuntimeScope
    (
        sessions: ISessionHostPort,
        journal: AgentJournal option,
        gitTreePort: GitTreePort option,
        workspaceDirectory: string option,
        sessionParents: Dictionary<string, string>,
        currentPhysicalUserMessage: string -> string option,
        verdictSessions: HashSet<string>,
        sessionDirectories: Dictionary<string, string>,
        onRunStarted: (SessionId -> AgentRole -> string option -> unit) option,
        backgroundFor: (string -> string option) option,
        snapshot: ISessionSnapshotPort option,
        cancelSignals: (SessionId seq -> unit) option,
        ?eventPort: IEventObservationPort
    ) =

    let gate = obj ()
    let runtimes = Dictionary<string, HostForkRuntime>()
    let executorRuntimes = Dictionary<string, HostForkRuntime>()
    let treePorts = Dictionary<string, GitTreePort>()
    let orchestratorHosts = Dictionary<string, OrchestratorHost>()
    let onCancelSignals = defaultArg cancelSignals ignore
    let onStarted = defaultArg onRunStarted (fun _ _ _ -> ())
    let background = defaultArg backgroundFor (fun _ -> None)
    let terminalPort = eventPort
    let mutable disposed = false

    let registerChild parentSid (_role: AgentRole) childId =
        sessionParents.[SessionId.value childId] <- parentSid

    let directoryFor sid =
        match sessionDirectories.TryGetValue sid with
        | true, path -> Some path
        | false, _ -> None

    let createRuntime sid =
        HostForkRuntime(
            SessionId.create sid,
            sessions,
            ?journal = journal,
            onChildCreated = (fun _ role childId -> registerChild sid role childId),
            onChildCreatedDir =
                (fun _ childId directory ->
                    directory
                    |> Option.iter (fun path -> sessionDirectories.[SessionId.value childId] <- path)),
            directoryFor = (fun _ -> directoryFor sid),
            onRunStarted = onStarted,
            parentWorkRecordFor = (fun parentId -> background (SessionId.value parentId)),
            childWorkRecordFor = (fun childId -> background (SessionId.value childId)),
            ?sessionSnapshot = snapshot,
            cancelSignals = onCancelSignals,
            // REVIEW-007: this runtime is a Manager's own fork surface, so a
            // Reviewer it forks gets its barrier opened here (see
            // `HostForkRuntimeFork.Fork`). The Orchestrator's runtime keeps this
            // off; ORCH-006 opens barriers at the reverify site.
            openReviewBarrier = true,
            treeHashFor =
                (fun agentId ->
                    // 主会话（无父）的目录从未经 RegisterChildDirectory 注册——
                    // 兜底主 workspace，否则 Manager 自己 fork 的 Reviewer 永远
                    // 打不开 barrier（REVIEW-008 fail closed 拒绝 verdict）。
                    directoryFor agentId
                    |> Option.orElse workspaceDirectory
                    |> Option.map (fun path ->
                        let tree = GitTree.create path

                        try
                            GitTreeHash.create (tree.GetTreeHash())
                        with _ ->
                            GitTreeHash.create "NO_HEAD_TREE"))
        )

    /// AGENT-007: the single Role source, or `None`.
    ///
    /// `None` means the tool set is empty — the clause says so explicitly, and
    /// names the "allow inspector while the role is unresolved" exemption the old
    /// code had as the thing to delete. A read-only tool executed under an
    /// unknown role is still an unauthorised execution.
    let roleFor (ctx: HostToolContext) =
        match journal with
        | Some durable when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
            PromptAuthorityLedger.activeProfile
                (SessionId.create ctx.SessionId)
                (AgentJournal.snapshot durable).AgentProjections
            |> Option.map (fun profile -> profile.CanonicalRole)
        | _ -> None

    /// The managed agent the Authority Root selected for this session.
    ///
    /// `SelectedAgent`, not `EffectiveAgent`: a PTY belongs to the Logical Run, and
    /// PROMPT-002 fixes SelectedAgent for its whole duration, whereas FALLBACK-002
    /// moves EffectiveAgent per attempt. A PTY labelled with whichever side the
    /// cursor happened to be on would change identity mid-run.
    let managedAgentFor (ctx: HostToolContext) =
        match journal with
        | Some durable when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
            PromptAuthorityLedger.activeProfile
                (SessionId.create ctx.SessionId)
                (AgentJournal.snapshot durable).AgentProjections
            |> Option.bind (fun profile -> ManagedAgent.tryParse profile.SelectedAgent)
        | _ -> None

    member _.Sessions = sessions
    member _.Journal = journal
    member _.Snapshot = snapshot
    member _.EventPort = terminalPort
    member _.WorkspaceDirectory = workspaceDirectory
    // REVIEW-010/HOST-012: deferred seal candidates, shared across instances.
    member _.PendingReviewSeals = SharedState.PendingReviewSeals

    /// EXEC-011: the administrator's ceiling on any single process.
    ///
    /// Resolved once per scope so every executor call in a session shares one
    /// ceiling. A non-positive or unparseable setting falls back to the default
    /// rather than being treated as "no limit": the clause requires the hard limit
    /// to be finite, so an unreadable configuration must not widen it.
    member val ProcessHardLimit =
        match Environment.GetEnvironmentVariable "WANXIANGSHU_PROCESS_HARD_LIMIT_SECS" with
        | null
        | "" -> ProcessEstimate.DefaultHardLimit
        | value ->
            match Double.TryParse value with
            | true, seconds when seconds > 0.0 && not (Double.IsInfinity seconds) -> TimeSpan.FromSeconds seconds
            | _ -> ProcessEstimate.DefaultHardLimit

    member _.SessionParents = sessionParents
    member _.CurrentPhysicalUserMessage(sessionId) = currentPhysicalUserMessage sessionId
    member _.DirectoryFor(sessionId) = directoryFor sessionId
    member _.BackgroundFor(sessionId) = background sessionId

    member _.RegisterDirectory(sessionId, path) = sessionDirectories.[sessionId] <- path

    member _.RoleFor(ctx: HostToolContext) = roleFor ctx

    /// AGENT-013 + PROMPT-008: the managed agent a PTY is opened for.
    member _.ManagedAgentFor(ctx: HostToolContext) = managedAgentFor ctx

    member this.IsRole(ctx: HostToolContext, expected: Role) = this.RoleFor ctx = Some expected

    member _.RuntimeFor(ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            Error "Missing sessionID"
        else
            lock gate (fun () ->
                if disposed then
                    Error "Tool runtime scope is disposed"
                else
                    match runtimes.TryGetValue ctx.SessionId with
                    | true, runtime when not runtime.IsCancelled -> Ok runtime
                    | _ ->
                        let runtime = createRuntime ctx.SessionId
                        runtimes.[ctx.SessionId] <- runtime
                        Ok runtime)

    member _.ExecutorRuntimeFor(ctx: HostToolContext) =
        lock gate (fun () ->
            match executorRuntimes.TryGetValue ctx.SessionId with
            | true, runtime when not runtime.IsCancelled -> runtime
            | _ ->
                let runtime =
                    HostForkRuntime(
                        SessionId.create ctx.SessionId,
                        sessions,
                        ?journal = journal,
                        onChildCreated = (fun _ role childId -> registerChild ctx.SessionId role childId)
                    )

                executorRuntimes.[ctx.SessionId] <- runtime
                runtime)

    member _.OrchestratorHostFor(sessionId: string) =
        lock gate (fun () ->
            match orchestratorHosts.TryGetValue sessionId with
            | true, host -> host
            | false, _ ->
                let host =
                    OrchestratorHost(
                        { Sessions = sessions
                          Journal = journal
                          SessionSnapshot = snapshot
                          OnChildCreated = fun _ role childId -> registerChild sessionId role childId
                          RegisterChildDirectory =
                            fun childId path -> sessionDirectories.[SessionId.value childId] <- path
                          RegisterReviewerTree = fun reviewerId port -> treePorts.[reviewerId] <- port
                          OnRunStarted = onStarted
                          RepoPath = defaultArg workspaceDirectory "."
                          TargetBranch = ""
                          ParentWorkRecordFor = (fun sid -> background (SessionId.value sid))
                          ChildWorkRecordFor = (fun sid -> background (SessionId.value sid)) },
                        SessionId.create sessionId
                    )

                orchestratorHosts.[sessionId] <- host
                host)

    member _.TreePortFor(reviewerId: string) =
        match treePorts.TryGetValue reviewerId with
        | true, port -> Some port
        | false, _ -> gitTreePort

    member _.MarkVerdictSubmitted(reviewerId: string) =
        lock gate (fun () -> verdictSessions.Add reviewerId |> ignore)

    member _.DisposeExecutorRuntime(sessionId: string) =
        lock gate (fun () ->
            match executorRuntimes.TryGetValue sessionId with
            | true, runtime ->
                executorRuntimes.Remove sessionId |> ignore
                runtime.Cancel()
            | false, _ -> ())

    member this.DisposeSession(sessionId: string) =
        lock gate (fun () ->
            match runtimes.TryGetValue sessionId with
            | true, runtime ->
                runtimes.Remove sessionId |> ignore
                runtime.Cancel()
            | false, _ -> ()

            this.DisposeExecutorRuntime sessionId

            orchestratorHosts.Remove sessionId |> ignore
            treePorts.Remove sessionId |> ignore)

    member this.Dispose() =
        let sessionIds =
            lock gate (fun () ->
                if disposed then
                    []
                else
                    disposed <- true
                    Seq.append runtimes.Keys executorRuntimes.Keys |> Seq.distinct |> Seq.toList)

        sessionIds |> List.iter this.DisposeSession

    interface ISessionRuntimeOwner with
        member this.DisposeSession sessionId = this.DisposeSession sessionId
        member this.DisposeExecutorRuntime sessionId = this.DisposeExecutorRuntime sessionId

    interface IDisposable with
        member this.Dispose() = this.Dispose()
