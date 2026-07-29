namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

module ToolSurface =

    open ToolSurfaceEmit
    open ToolSurfacePty
    open ToolSurfaceFork
    open ToolSurfaceJoin

    [<Emit("Object.defineProperty($0, $1, { value: $2, enumerable: false })")>]
    let private defineHidden (target: obj) (name: string) (value: obj) : unit = jsNative

    let private mkSid (s: string) = SessionId.create s

    let create
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (workspaceDirectory: string option)
        (sessionParents: Dictionary<string, string>)
        (sessionRoles: Dictionary<string, string>)
        (currentPhysicalUserMessage: string -> string option)
        (verdictSessions: HashSet<string>)
        (sessionDirectories: Dictionary<string, string>)
        (onRunStarted: (SessionId -> AgentRole -> string option -> unit) option)
        (backgroundBFor: (string -> string option) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        : obj =
        let factory = toolModule?tool
        let runtimes = Dictionary<string, HostForkRuntime>()
        let executorRuntimes = Dictionary<string, HostForkRuntime>()
        let reviewerHosts = Dictionary<string, ReviewerHost>()
        let worktreeTreePorts = Dictionary<string, GitTreePort>()
        let orchestratorHosts = Dictionary<string, OrchestratorHost>()
        let gate = obj ()
        let onCancelSignals = defaultArg cancelSignals (fun _ -> ())

        let orchestratorHostFor (sid: string) =
            ToolSurfaceOrchestrator.hostFor
                { Sessions = sessionPort
                  Journal = journal
                  WorkspaceDirectory = workspaceDirectory
                  SessionParents = sessionParents
                  SessionRoles = sessionRoles
                  SessionDirectories = sessionDirectories
                  TreePorts = worktreeTreePorts
                  OnRunStarted = onRunStarted }
                gate
                orchestratorHosts
                sid

        let createRuntime (sid: string) =
            HostForkRuntime(
                mkSid sid,
                sessionPort,
                ?journal = journal,
                onChildCreated =
                    (fun _ role childId ->
                        ToolSurfaceOrchestrator.registerChild sessionParents sessionRoles sid role childId),
                onChildCreatedDir =
                    (fun _ childId dirOpt ->
                        dirOpt
                        |> Option.iter (fun d -> sessionDirectories.[SessionId.value childId] <- d)),
                directoryFor =
                    (fun _ ->
                        match sessionDirectories.TryGetValue sid with
                        | true, path -> Some path
                        | false, _ -> None),
                ?onRunStarted = onRunStarted,
                parentWorkRecordFor =
                    (fun sessionId ->
                        match backgroundBFor with
                        | Some fn -> fn (SessionId.value sessionId)
                        | None -> None),
                childWorkRecordFor =
                    (fun sessionId ->
                        match backgroundBFor with
                        | Some fn -> fn (SessionId.value sessionId)
                        | None -> None),
                ?sessionSnapshot = snapshot,
                cancelFallbackRetries =
                    (fun ids -> ids |> Seq.iter PluginFallbackRetry.cancelPendingFor),
                cancelSignals = onCancelSignals
            )

        let runtimeFor (ctx: obj) =
            let sid =
                if isNull ctx || isNull ctx?sessionID then
                    ""
                else
                    unbox<string> ctx?sessionID

            if String.IsNullOrWhiteSpace sid then
                Error "Missing sessionID"
            else
                Ok(
                    lock gate (fun () ->
                        match runtimes.TryGetValue sid with
                        | true, runtime when not runtime.IsCancelled -> runtime
                        | _ ->
                            // Esc tears down the current physical runtime. A later user turn
                            // needs a fresh resource, never a resurrected child or a dead cache entry.
                            let runtime = createRuntime sid
                            runtimes.[sid] <- runtime
                            runtime)
                )

        let ptyDeps =
            { SessionRoles = sessionRoles
              SessionDirectories = sessionDirectories
              WorkspaceDirectory = workspaceDirectory
              RuntimeFor = runtimeFor
              OrchestratorHostFor = orchestratorHostFor }

        let forkExecute = ToolSurfaceFork.forkExecute ptyDeps
        let forkPtyExecute = ToolSurfacePty.forkPtyExecute ptyDeps
        let joinExecute = ToolSurfaceJoin.joinExecute ptyDeps
        let listExecute = ToolSurfaceJoin.listExecute ptyDeps

        let verdictExecute =
            VerdictSurface.create
                sessionParents
                sessionRoles
                currentPhysicalUserMessage
                journal
                (fun reviewerId ->
                    match worktreeTreePorts.TryGetValue reviewerId with
                    | true, port -> Some port
                    | false, _ -> gitTreePort)
                reviewerHosts
                verdictSessions
                snapshot

        let managerForkArgs, devopsPtyArgs, orchestratorManagerJobArgs, verdictArgs, definition =
            ToolSurfaceOrchestrator.toolDefBuilders factory

        let executorRuntimeFor =
            ToolSurfaceOrchestrator.executorRuntimeFor
                gate
                executorRuntimes
                mkSid
                sessionPort
                sessionParents
                sessionRoles

        let disposeExecutorRuntime =
            ToolSurfaceOrchestrator.disposeExecutorRuntime gate executorRuntimes

        let sessionDirFor (sid: string) =
            match sessionDirectories.TryGetValue sid with
            | true, d -> Some d
            | false, _ -> None

        let registerChildDir (childId: string) (dir: string) = sessionDirectories.[childId] <- dir

        let executor =
            ExecutorTool.create toolModule runtimeFor executorRuntimeFor workspaceDirectory (Some sessionDirFor)

        let inspector =
            InspectorTool.create
                toolModule
                sessionPort
                backgroundBFor
                (Some sessionDirFor)
                (Some registerChildDir)
                journal

        let coder =
            CoderTool.create toolModule sessionPort backgroundBFor (Some sessionDirFor) (Some registerChildDir) journal

        // Manager: fork. DevOps: fork-pty. Orchestrator: fork-manager.
        // Names differ because schemas conflict.
        let tools =
            createObj
                [ "fork", box (applyTool factory (definition "Fork or nudge an agent" managerForkArgs forkExecute))
                  "fork-pty",
                  box (
                      applyTool factory (definition "Create, write, read, or signal a PTY" devopsPtyArgs forkPtyExecute)
                  )
                  "fork-manager",
                  box (applyTool factory (definition "Fork a manager job" orchestratorManagerJobArgs forkExecute))
                  "join",
                  box (applyTool factory (definition "Wait for any agent or PTY completion" (createObj []) joinExecute))
                  "list", box (applyTool factory (definition "List active agents and PTYs" (createObj []) listExecute))
                  "verdict", box (applyTool factory (definition "Submit the review verdict" verdictArgs verdictExecute))
                  "executor", executor
                  "inspector", inspector
                  "coder", coder ]

        defineHidden tools "disposeExecutorRuntime" (box disposeExecutorRuntime)
        tools
