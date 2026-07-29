namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// Owns every per-session tool runtime. Role authorization comes from Prompt
/// Authority (or the typed Host context when no journal exists), never the
/// display-only sessionRoles cache.
type ToolRuntimeScope
    (
        sessions: ISessionHostPort,
        journal: AgentJournal option,
        gitTreePort: GitTreePort option,
        workspaceDirectory: string option,
        sessionParents: Dictionary<string, string>,
        sessionRoles: Dictionary<string, string>,
        currentPhysicalUserMessage: string -> string option,
        verdictSessions: HashSet<string>,
        sessionDirectories: Dictionary<string, string>,
        onRunStarted: (SessionId -> AgentRole -> string option -> unit) option,
        backgroundFor: (string -> string option) option,
        snapshot: ISessionSnapshotPort option,
        cancelSignals: (SessionId seq -> unit) option
    ) =

    let gate = obj ()
    let runtimes = Dictionary<string, HostForkRuntime>()
    let executorRuntimes = Dictionary<string, HostForkRuntime>()
    let reviewerHosts = Dictionary<string, ReviewerHost>()
    let treePorts = Dictionary<string, GitTreePort>()
    let orchestratorHosts = Dictionary<string, OrchestratorHost>()
    let onCancelSignals = defaultArg cancelSignals ignore
    let onStarted = defaultArg onRunStarted (fun _ _ _ -> ())
    let background = defaultArg backgroundFor (fun _ -> None)
    let mutable disposed = false

    let registerChild parentSid role childId =
        let childKey = SessionId.value childId
        sessionParents.[childKey] <- parentSid
        // Display/strict-mock cache; RoleFor also consults it as a runtime role hint.
        sessionRoles.[childKey] <- role.ToString().ToLowerInvariant()

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
            cancelFallbackRetries = (fun ids -> ids |> Seq.iter PluginFallbackRetry.cancelPendingFor),
            cancelSignals = onCancelSignals
        )

    let roleName (ctx: HostToolContext) =
        let fromAuthority =
            match journal with
            | None -> None
            | Some durable when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                let projection = AgentJournal.snapshot durable

                PromptAuthorityLedger.activeProfile (SessionId.create ctx.SessionId) projection.AgentProjections
                |> Option.map (fun profile -> PromptAuthority.roleLabel profile.CanonicalRole)
            | Some _ -> None

        let fromSessionRoles () =
            if String.IsNullOrWhiteSpace ctx.SessionId then
                None
            else
                match sessionRoles.TryGetValue ctx.SessionId with
                | true, role when not (String.IsNullOrWhiteSpace role) -> Some role
                | _ -> None

        fromAuthority
        |> Option.orElseWith fromSessionRoles
        |> Option.orElseWith (fun () ->
            ctx.Agent
            |> Option.bind (fun agent ->
                ManagedAgent.tryParse agent
                |> Option.map (fun managed -> ManagedAgent.roleName managed.Role)
                |> Option.orElseWith (fun () -> HostSessionContext.canonicalRole agent)))

    let roleOfName (name: string) =
        match PromptAuthority.tryParseRole name with
        | Some role -> Some role
        | None ->
            AgentRoleHelpers.roleOfString name
            |> Option.map AgentRoleHelpers.toRole

    member _.Sessions = sessions
    member _.Journal = journal
    member _.Snapshot = snapshot
    member _.WorkspaceDirectory = workspaceDirectory
    member _.SessionParents = sessionParents
    member _.CurrentPhysicalUserMessage(sessionId) = currentPhysicalUserMessage sessionId
    member _.DirectoryFor(sessionId) = directoryFor sessionId
    member _.BackgroundFor(sessionId) = background sessionId

    member _.RegisterDirectory(sessionId, path) = sessionDirectories.[sessionId] <- path

    member _.RoleFor(ctx: HostToolContext) = roleName ctx |> Option.bind roleOfName

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
                        ?journal = None,
                        onChildCreated = (fun _ role childId -> registerChild ctx.SessionId role childId),
                        cancelFallbackRetries = (fun ids -> ids |> Seq.iter PluginFallbackRetry.cancelPendingFor)
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

    member _.ReviewerHostFor(reviewerId: string, managerId: string, treePort: GitTreePort) =
        lock gate (fun () ->
            match reviewerHosts.TryGetValue reviewerId with
            | true, host -> host
            | false, _ ->
                match journal with
                | None -> invalidOp "Reviewer journal is unavailable"
                | Some durable ->
                    let host =
                        ReviewerHost(
                            durable,
                            SessionId.create managerId,
                            SessionId.create reviewerId,
                            gitTreePort = treePort
                        )

                    reviewerHosts.[reviewerId] <- host
                    host)

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

            reviewerHosts.Remove sessionId |> ignore
            orchestratorHosts.Remove sessionId |> ignore
            treePorts.Remove sessionId |> ignore)

    member this.Dispose() =
        let sessionIds =
            lock gate (fun () ->
                if disposed then []
                else
                    disposed <- true
                    Seq.append runtimes.Keys executorRuntimes.Keys |> Seq.distinct |> Seq.toList)

        sessionIds |> List.iter this.DisposeSession

    interface ISessionRuntimeOwner with
        member this.DisposeSession sessionId = this.DisposeSession sessionId
        member this.DisposeExecutorRuntime sessionId = this.DisposeExecutorRuntime sessionId

    interface IDisposable with
        member this.Dispose() = this.Dispose()
