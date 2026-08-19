namespace Wanxiangshu.OpenCode

open Wanxiangshu.Composition.Durable

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
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
        verdictSubmissions: HashSet<string>,
        sessionDirectories: Dictionary<string, string>,
        onRunStarted: (SessionId -> Role -> string option -> unit) option,
        parentWorkRecordFor: (string -> Task<string option>) option,
        childWorkRecordFor: (string -> Task<string option>) option,
        snapshot: ISessionSnapshotPort option,
        cancelSignals: (SessionId seq -> unit) option,
        ?eventPort: IEventObservationPort,
        ?finalityReviewerTimeoutMs: int
    ) =

    let gate = obj ()
    let ownedWorkGate = obj ()
    // DSL-MUTABLE: resource — per-owner fork runtime registry
    let runtimes = Dictionary<string, HostForkRuntime>()
    // DSL-MUTABLE: resource — per-session executor runtime registry
    let executorRuntimes = Dictionary<string, HostForkRuntime>()
    // DSL-MUTABLE: resource — reviewer tree port registry
    let treePorts = Dictionary<string, GitTreePort>()
    // DSL-MUTABLE: resource — per-session orchestrator host registry
    let orchestratorHosts = Dictionary<string, OrchestratorHost>()
    let onCancelSignals = defaultArg cancelSignals ignore
    let onStarted = defaultArg onRunStarted (fun _ _ _ -> ())
    // COMPANION-003: parent→child keeps Opening; child→parent omits it (includeOpening=false).
    let parentRecord = defaultArg parentWorkRecordFor (fun _ -> Task.FromResult None)
    let childRecord = defaultArg childWorkRecordFor (fun _ -> Task.FromResult None)
    let terminalPort = eventPort
    let finalityTimeoutMs = finalityReviewerTimeoutMs
    // DSL-MUTABLE: resource — tool runtime dispose latch
    let mutable disposed = false
    // DSL-MUTABLE: resource — async tool-callback admission latch.
    let mutable acceptingOwnedWork = true
    // DSL-MUTABLE: resource — in-flight tool-owned callback count.
    let mutable ownedWorkCount = 0
    // DSL-MUTABLE: single-flight — shared waiter for tool-owned callback drain.
    let mutable ownedWorkDrainWaiter: TaskCompletionSource<unit> option = None
    // DSL-MUTABLE: resource — first tool-owned callback failure for shutdown propagation.
    let mutable ownedWorkFailure: exn option = None
    /// P0-RECOVERY-JOIN-001: family recovery before join / publish consume.
    // DSL-MUTABLE: resource — family recovery callback attachment
    let mutable familyRecovery: (SessionId -> Task<FamilyRecovery>) option = None

    /// EXEC-017 attempt-scoped join interrupt registry (PluginRuntimeScope or local default).
    // DSL-MUTABLE: resource — join attempt registry attachment
    let mutable joinAttempts: IJoinAttemptRegistry =
        JoinAttemptRegistry() :> IJoinAttemptRegistry

    let finishOwnedWork () =
        lock ownedWorkGate (fun () ->
            ownedWorkCount <- ownedWorkCount - 1

            if not acceptingOwnedWork && ownedWorkCount = 0 then
                ownedWorkDrainWaiter
                |> Option.iter (fun waiter -> AsyncSupport.trySetResult waiter () |> ignore))

    let ownedWorkDrainTask () : Task =
        match ownedWorkDrainWaiter with
        | Some waiter -> waiter.Task :> Task
        | None ->
            let waiter =
                TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

            ownedWorkDrainWaiter <- Some waiter
            waiter.Task :> Task

    let stopOwnedWorkAndDrain () : Task<exn option> =
        let waiting =
            lock ownedWorkGate (fun () ->
                acceptingOwnedWork <- false

                if ownedWorkCount = 0 then
                    Task.FromResult(()) :> Task
                else
                    ownedWorkDrainTask ())

        task {
            do! waiting
            return lock ownedWorkGate (fun () -> ownedWorkFailure)
        }

    let recordOwnedWorkFailure (failure: exn) =
        lock ownedWorkGate (fun () -> ownedWorkFailure <- Option.orElse ownedWorkFailure (Some failure))

    let captureOwnedWorkFailure (start: unit -> Task) : Task<exn option> =
        task {
            try
                do! start ()
                return None
            with ex ->
                return Some ex
        }

    let observeOwnedWork (start: unit -> Task) : Task =
        task {
            let! failure = captureOwnedWorkFailure start
            failure |> Option.iter recordOwnedWorkFailure
            finishOwnedWork ()
        }
        :> Task

    let addUniqueForkRuntime (owned: ResizeArray<HostForkRuntime>) (runtime: HostForkRuntime) =
        if not (owned |> Seq.exists (fun current -> obj.ReferenceEquals(current, runtime))) then
            owned.Add runtime

    let registerChild parentSid (_role: Role) childId =
        sessionParents.[SessionId.value childId] <- parentSid

    let directoryFor sid =
        match sessionDirectories.TryGetValue sid with
        // ORCH-006 defence: a manager-family directory is the worktree, which is
        // removed at publish. A request that races the release would otherwise keep
        // pointing at the deleted path and lose the AGENTS.md instruction block
        // (ARCH-004 seal break, measured on orchestrator-publish under concurrency).
        // Verify the path still exists; fall back to None (root workspace) when the
        // worktree is gone — the residual guard-round request has no worktree work
        // left to do.
        | true, path when System.IO.Directory.Exists path -> Some path
        | _ -> None

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
            parentWorkRecordFor = (fun parentId -> parentRecord (SessionId.value parentId)),
            childWorkRecordFor = (fun childId -> childRecord (SessionId.value childId)),
            ?sessionSnapshot = snapshot,
            cancelSignals = onCancelSignals,
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

    let getOrCreateRuntime ownerKey =
        lock gate (fun () ->
            match disposed, runtimes.TryGetValue ownerKey with
            | true, _ -> Error "Tool runtime scope is disposed"
            | false, (true, runtime) when not runtime.IsCancelled -> Ok runtime
            | false, _ ->
                let runtime = createRuntime ownerKey
                runtimes.[ownerKey] <- runtime
                Ok runtime)

    let resumableAgentId (record: HandleRecord) =
        match record.Ownership, HandleId.tryAgent record.Handle with
        | HandleOwnership.HostOwnedHidden, _ -> Error "host-owned hidden child is not resumable by the user session"
        | HandleOwnership.DurableParentHandle, None -> Error "non-agent durable handle is not resumable"
        | HandleOwnership.DurableParentHandle, Some handleId -> Ok(AgentHandleId.value handleId)

    let adoptExistingIntoRuntime (parentSessionId: SessionId) (record: HandleRecord) agentId =
        getOrCreateRuntime (SessionId.value parentSessionId)
        |> Result.map (fun runtime ->
            runtime.AdoptExisting(
                agentId,
                record.ChildSessionId,
                AgentRoleIdentity.ofRole record.CanonicalRole,
                record.TargetAgent
            ))

    let sessionIdOf (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            None
        else
            Some(SessionId.create ctx.SessionId)

    let logicalOwnerFor (sessionId: SessionId) =
        FissionRuntime.tryOwner sessionId
        |> Option.orElseWith (fun () ->
            journal
            |> Option.bind (fun durable ->
                FissionProjection.tryOwnerOfLane sessionId (AgentJournal.snapshot durable).AgentProjections.Fission))
        |> Option.defaultValue sessionId

    let activeProfileFor sessionId =
        match journal with
        | Some durable ->
            let projections = (AgentJournal.snapshot durable).AgentProjections

            PromptAuthorityLedger.activeProfile sessionId projections
            |> Option.orElseWith (fun () -> PromptAuthorityLedger.lastAuthorityProfile sessionId projections)
        | None -> None

    /// AGENT-007: the single Role source, or `None`.
    ///
    /// `None` means the tool set is empty — the clause says so explicitly, and
    /// names the "allow inspector while the role is unresolved" exemption the old
    /// code had as the thing to delete. A read-only tool executed under an
    /// unknown role is still an unauthorised execution.
    let roleFor (ctx: HostToolContext) =
        sessionIdOf ctx
        |> Option.bind activeProfileFor
        |> Option.map (fun profile -> profile.CanonicalRole)

    let acceptNamedHumanRoot
        (durable: AgentJournal)
        (sessionId: SessionId)
        (user: SessionMessage)
        (agent: string)
        : Task<Role option> =
        task {
            let runtime = PromptDispatcher.forJournal durable

            match! runtime.AcceptHumanRoot sessionId (PhysicalUserMessageId.create user.Id) (Some agent) with
            | Ok profile -> return Some profile.CanonicalRole
            | Error _ -> return None
        }

    let acceptHumanRootFromUser
        (durable: AgentJournal)
        (sessionId: SessionId)
        (user: SessionMessage)
        : Task<Role option> =
        match user.Agent with
        | None -> Task.FromResult None
        | Some agent -> acceptNamedHumanRoot durable sessionId user agent

    let acceptUnkeyedParentUser
        (durable: AgentJournal)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (messages: SessionMessage list)
        : Task<Role option> =
        let providerRunId = ProviderRunIdentity.value providerRun

        let parentUser =
            messages
            |> List.tryFind (fun message -> message.Id = providerRunId)
            |> Option.bind (fun assistant -> assistant.ParentId)
            |> Option.bind (fun parentId ->
                messages
                |> List.tryFind (fun message -> message.Id = parentId && message.Role = "user"))

        match parentUser with
        | Some user when user.PromptKey.IsNone -> acceptHumanRootFromUser durable sessionId user
        | _ -> Task.FromResult None

    let recoverFromSnapshotMessages
        (durable: AgentJournal)
        (snapshotPort: ISessionSnapshotPort)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        : Task<Role option> =
        task {
            match! snapshotPort.GetMessages sessionId with
            | Error _ -> return None
            | Ok messages -> return! acceptUnkeyedParentUser durable sessionId providerRun messages
        }

    let recoverWithPorts
        (durable: AgentJournal)
        (snapshotPort: ISessionSnapshotPort)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        : Task<Role option> =
        task {
            match activeProfileFor sessionId with
            | Some profile -> return Some profile.CanonicalRole
            | None -> return! recoverFromSnapshotMessages durable snapshotPort sessionId providerRun
        }

    let recoverHumanRootFromSnapshot (ctx: HostToolContext) =
        task {
            match journal, snapshot, sessionIdOf ctx, ctx.ProviderRunId with
            | Some durable, Some snapshotPort, Some sessionId, Some providerRun ->
                return! recoverWithPorts durable snapshotPort sessionId providerRun
            | _ -> return None
        }

    let ensureRoleFor ctx =
        task {
            match roleFor ctx with
            | Some role -> return Some role
            | None -> return! recoverHumanRootFromSnapshot ctx
        }

    /// The managed agent the Authority Root selected for this session.
    ///
    /// `SelectedAgent`, not `EffectiveAgent`: a PTY belongs to the Logical Run, and
    /// PROMPT-002 fixes SelectedAgent for its whole duration, whereas FALLBACK-002
    /// moves EffectiveAgent per attempt. A PTY labelled with whichever side the
    /// cursor happened to be on would change identity mid-run.
    let managedAgentFor (ctx: HostToolContext) =
        sessionIdOf ctx
        |> Option.bind activeProfileFor
        |> Option.bind (fun profile -> ManagedAgent.tryParse profile.SelectedAgent)

    let parseProcessHardLimit (value: string) =
        match Double.TryParse value with
        | true, seconds when seconds > 0.0 && not (Double.IsInfinity seconds) -> TimeSpan.FromSeconds seconds
        | _ -> ProcessEstimate.DefaultHardLimit

    member _.FinalityReviewerTimeoutMs = finalityTimeoutMs
    member _.Sessions = sessions
    member _.Journal = journal
    member _.Snapshot = snapshot
    member _.EventPort = terminalPort
    member _.WorkspaceDirectory = workspaceDirectory
    member _.ActiveProfileFor(sessionId: SessionId) = activeProfileFor sessionId
    /// GLORY-003: the run-started callback wired by the plugin bootstrap, exposed
    /// so the Finality workflow's hidden Reviewer binds the same reconciler.
    member _.RunStarted = onStarted

    /// EXEC-011: the administrator's ceiling on any single process.
    ///
    /// Resolved once per scope so every executor call in a session shares one
    /// ceiling. A non-positive or unparseable setting falls back to the default
    /// rather than being treated as "no limit": the clause requires the hard limit
    /// to be finite, so an unreadable configuration must not widen it.
    // DSL-MUTABLE: resource — resolved process hard limit ceiling
    member val ProcessHardLimit =
        match Environment.GetEnvironmentVariable "WANXIANGSHU_PROCESS_HARD_LIMIT_SECS" with
        | null
        | "" -> ProcessEstimate.DefaultHardLimit
        | value -> parseProcessHardLimit value

    member _.SessionParents = sessionParents
    member _.CurrentPhysicalUserMessage(sessionId) = currentPhysicalUserMessage sessionId
    member _.DirectoryFor(sessionId) = directoryFor sessionId
    member _.LogicalOwnerFor(sessionId: SessionId) = logicalOwnerFor sessionId

    member _.RegisterPhysicalParent(sessionId: SessionId, parentId: SessionId option) =
        match parentId with
        | Some parent -> sessionParents.[SessionId.value sessionId] <- SessionId.value parent
        | None -> sessionParents.Remove(SessionId.value sessionId) |> ignore

    member _.ParentWorkRecordFor(sessionId) = parentRecord sessionId
    member _.ChildWorkRecordFor(sessionId) = childRecord sessionId

    member _.RegisterDirectory(sessionId, path) = sessionDirectories.[sessionId] <- path

    member _.RoleFor(ctx: HostToolContext) = roleFor ctx
    member _.EnsureRoleFor(ctx: HostToolContext) = ensureRoleFor ctx

    /// AGENT-013 + PROMPT-008: the managed agent a PTY is opened for.
    member _.ManagedAgentFor(ctx: HostToolContext) = managedAgentFor ctx

    member this.IsRole(ctx: HostToolContext, expected: Role) = this.RoleFor ctx = Some expected

    /// Wire PluginRuntimeScope.RequireFamilyRecovery (or test double).
    member _.AttachFamilyRecovery(fn: SessionId -> Task<FamilyRecovery>) = familyRecovery <- Some fn

    /// EXEC-017: share PluginRuntimeScope.JoinAttempts with JoinTool.
    member _.AttachJoinAttempts(registry: IJoinAttemptRegistry) = joinAttempts <- registry

    member _.JoinAttempts = joinAttempts

    /// P0-RECOVERY-JOIN-001: join / JoinPublishedAvailable require FamilyReady. Missing attach → FamilyBlocked.
    member _.RequireFamilyRecovery(root: SessionId) : Task<FamilyRecovery> =
        task {
            match familyRecovery with
            | None ->
                return FamilyRecovery.FamilyBlocked(NonEmpty.one (RecoveryBlock.RecoveryCoordinatorUnavailable root))
            | Some fn -> return! fn root
        }

    member _.RuntimeFor(ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            Error "Missing sessionID"
        else
            SessionId.create ctx.SessionId
            |> logicalOwnerFor
            |> SessionId.value
            |> getOrCreateRuntime

    /// CRASH-018: process-local adoption for explicit /continue. The durable
    /// handle stays byte-for-byte as it was at the crash boundary; a later LLM
    /// fork reuse is the first action allowed to reopen it durably.
    member _.AdoptExistingChild(parentSessionId: SessionId, record: HandleRecord) : Result<unit, string> =
        resumableAgentId record
        |> Result.bind (adoptExistingIntoRuntime parentSessionId record)

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
                        onChildCreated = (fun _ role childId -> registerChild ctx.SessionId role childId),
                        // EXEC-014: map/reduce Distiller children are Host-owned and
                        // parent-invisible. A DurableParentHandle would leak every
                        // worker into the caller's list/join/guard (EXEC-016) and
                        // block suicide with "join before end" long after `run`
                        // returned the distilled summary.
                        ownership = HandleOwnership.HostOwnedHidden
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
                          ParentWorkRecordFor = (fun sid -> parentRecord (SessionId.value sid))
                          ChildWorkRecordFor = (fun sid -> childRecord (SessionId.value sid)) },
                        SessionId.create sessionId
                    )

                orchestratorHosts.[sessionId] <- host
                host)

    member _.TreePortFor(reviewerId: string) =
        match treePorts.TryGetValue reviewerId with
        | true, port -> Some port
        | false, _ -> gitTreePort

    member _.MarkVerdictSubmitted(reviewerId: string, physicalUserMessageId: PhysicalUserMessageId) =
        lock gate (fun () ->
            let sessionId = SessionId.create reviewerId

            verdictSubmissions
            |> Seq.filter (JudgementRequestIdentity.belongsTo sessionId)
            |> Seq.toArray
            |> Array.iter (fun existing -> verdictSubmissions.Remove existing |> ignore)

            JudgementRequestIdentity.key sessionId physicalUserMessageId
            |> verdictSubmissions.Add
            |> ignore)

    member _.HasVerdictSubmitted(reviewerId: string, physicalUserMessageId: PhysicalUserMessageId) =
        lock gate (fun () ->
            JudgementRequestIdentity.key (SessionId.create reviewerId) physicalUserMessageId
            |> verdictSubmissions.Contains)

    member _.RunOwnedWork(start: unit -> Task) : bool =
        let admitted =
            lock ownedWorkGate (fun () ->
                if not acceptingOwnedWork then
                    false
                else
                    ownedWorkCount <- ownedWorkCount + 1
                    true)

        if admitted then
            observeOwnedWork start |> ignore

        admitted

    member _.DisposeExecutorRuntime(sessionId: string) : Task =
        let runtime =
            lock gate (fun () ->
                match executorRuntimes.TryGetValue sessionId with
                | true, active ->
                    executorRuntimes.Remove sessionId |> ignore
                    Some active
                | false, _ -> None)

        match runtime with
        | Some active -> active.CancelAndDrain()
        | None -> Task.FromResult(()) :> Task

    /// EXEC-016: live PTY on the parent fork runtime (not Executor runtime).
    member _.HasLivePty(sessionId: string) : bool =
        lock gate (fun () ->
            match runtimes.TryGetValue sessionId with
            | true, runtime when not runtime.IsCancelled ->
                let _, ptys = runtime.List()
                not (List.isEmpty ptys)
            | _ -> false)

    member _.CancelSessionChildren(sessionId: string) : Task =
        let forkRuntimes, orchestrator =
            lock gate (fun () ->
                let owned = ResizeArray<HostForkRuntime>()

                match runtimes.TryGetValue sessionId with
                | true, runtime ->
                    runtimes.Remove sessionId |> ignore
                    owned.Add runtime
                | false, _ -> ()

                match executorRuntimes.TryGetValue sessionId with
                | true, runtime ->
                    executorRuntimes.Remove sessionId |> ignore
                    addUniqueForkRuntime owned runtime
                | false, _ -> ()

                let host =
                    match orchestratorHosts.TryGetValue sessionId with
                    | true, current ->
                        orchestratorHosts.Remove sessionId |> ignore
                        Some current
                    | false, _ -> None

                owned |> Seq.toList, host)

        task {
            for runtime in forkRuntimes do
                do! runtime.CancelAndDrain()

            match orchestrator with
            | Some host -> do! host.CancelAndDrain()
            | None -> ()
        }
        :> Task

    member _.DisposeSession(sessionId: string) : Task =
        let forkRuntimes, orchestrator =
            lock gate (fun () ->
                // DSL-MUTABLE: algorithm-scratch — owned runtime accumulator for dispose
                let owned = ResizeArray<HostForkRuntime>()

                match runtimes.TryGetValue sessionId with
                | true, runtime ->
                    runtimes.Remove sessionId |> ignore
                    owned.Add runtime
                | false, _ -> ()

                match executorRuntimes.TryGetValue sessionId with
                | true, runtime ->
                    executorRuntimes.Remove sessionId |> ignore
                    addUniqueForkRuntime owned runtime
                | false, _ -> ()

                let host =
                    match orchestratorHosts.TryGetValue sessionId with
                    | true, current ->
                        orchestratorHosts.Remove sessionId |> ignore
                        Some current
                    | false, _ -> None

                treePorts.Remove sessionId |> ignore
                owned |> Seq.toList, host)

        task {
            for runtime in forkRuntimes do
                do! runtime.CancelAndDrain()

            match orchestrator with
            | Some host -> do! host.CancelAndDrain()
            | None -> ()
        }
        :> Task

    member _.DisposeAsync() : Task =
        let forkRuntimes, orchestrators =
            lock gate (fun () ->
                if disposed then
                    [], []
                else
                    disposed <- true

                    let ownedForkRuntimes =
                        Seq.append runtimes.Values executorRuntimes.Values |> Seq.distinct |> Seq.toList

                    let ownedOrchestrators = orchestratorHosts.Values |> Seq.toList

                    runtimes.Clear()
                    executorRuntimes.Clear()
                    orchestratorHosts.Clear()
                    treePorts.Clear()

                    ownedForkRuntimes, ownedOrchestrators)

        task {
            let! ownedFailure = stopOwnedWorkAndDrain ()

            for runtime in forkRuntimes do
                do! runtime.CancelAndDrain()

            for host in orchestrators do
                do! host.CancelAndDrain()

            match ownedFailure with
            | Some failure -> return raise failure
            | None -> return ()
        }
        :> Task

    member this.Dispose() = this.DisposeAsync() |> ignore

    interface ISessionRuntimeOwner with
        member this.CancelSessionChildren sessionId = this.CancelSessionChildren sessionId
        member this.DisposeSession sessionId = this.DisposeSession sessionId
        member this.DisposeExecutorRuntime sessionId = this.DisposeExecutorRuntime sessionId
        member this.DisposeAsync() = this.DisposeAsync()
        member this.HasLivePty sessionId = this.HasLivePty sessionId

    interface IDisposable with
        member this.Dispose() = this.Dispose()
