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

    [<RequireQualifiedAccess>]
    module private ForkField =
        [<Literal>]
        let Agent = "agent"

        [<Literal>]
        let Prompt = "prompt"

        [<Literal>]
        let Signal = "signal"

    [<RequireQualifiedAccess>]
    module private ListKind =
        [<Literal>]
        let Agent = "agent"

        [<Literal>]
        let Pty = "pty"

    let private mkSid (s: string) = SessionId.create s

    let create
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (workspaceDirectory: string option)
        (sessionParents: Dictionary<string, string>)
        (sessionRoles: Dictionary<string, string>)
        (verdictSessions: HashSet<string>)
        (modelConfig: ModelResolver.ModelConfig option)
        : obj =
        let factory = toolModule?tool
        let runtimes = Dictionary<string, HostForkRuntime>()
        let executorRuntimes = Dictionary<string, HostForkRuntime>()
        let reviewerHosts = Dictionary<string, ReviewerHost>()
        let worktreeTreePorts = Dictionary<string, GitTreePort>()
        let orchestratorHosts = Dictionary<string, OrchestratorHost>()
        let gate = obj ()

        let orchestratorHostFor (sid: string) =
            ToolSurfaceOrchestrator.hostFor
                { Sessions = sessionPort
                  Journal = journal
                  ModelConfig = modelConfig
                  WorkspaceDirectory = workspaceDirectory
                  SessionParents = sessionParents
                  SessionRoles = sessionRoles
                  TreePorts = worktreeTreePorts }
                gate
                orchestratorHosts
                sid

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
                        | true, r -> r
                        | false, _ ->
                            let r =
                                HostForkRuntime(
                                    mkSid sid,
                                    sessionPort,
                                    ?journal = journal,
                                    onChildCreated =
                                        (fun _ role childId ->
                                            ToolSurfaceOrchestrator.registerChild
                                                sessionParents
                                                sessionRoles
                                                sid
                                                role
                                                childId),
                                    ?modelResolver = modelConfig
                                )

                            runtimes.[sid] <- r
                            r)
                )

        let forkExecute (args: obj) (ctx: obj) =
            task {
                let agent = textArg args ForkField.Agent
                let prompt = textArg args ForkField.Prompt
                let signalText = optionalTextArg args ForkField.Signal

                match runtimeFor ctx with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let parsedSignal =
                        match signalText with
                        | None -> Ok None
                        | Some value -> PtySignal.tryParse value |> Result.map Some

                    match parsedSignal with
                    | Error err -> return box (stringify (createObj [ "error", box err ]))
                    | Ok signalOpt ->
                        match runtime.TryPty agent with
                        | Some ptyId ->
                            let! result = runtime.SendPty(ptyId, prompt, signalOpt)

                            match result with
                            | Ok read ->
                                return
                                    box (
                                        stringify (
                                            createObj
                                                [ "ptyId", box read.Id.Value
                                                  "output", box read.Output
                                                  "closed", box read.Closed ]
                                        )
                                    )
                            | Error err -> return box (stringify (createObj [ "error", box err ]))
                        | None when agent = Pty.AgentName ->
                            match signalOpt with
                            | Some _ ->
                                return
                                    box (stringify (createObj [ "error", box "PTY creation does not accept signal" ]))
                            | None ->
                                let! result = runtime.ForkPty(prompt, ?cwd = workspaceDirectory)

                                match result with
                                | Ok id ->
                                    return
                                        box (
                                            stringify (
                                                createObj
                                                    [ "ptyId", box id.Value; "output", box ""; "closed", box false ]
                                            )
                                        )
                                | Error err -> return box (stringify (createObj [ "error", box err ]))
                        | None ->
                            match signalOpt with
                            | Some _ ->
                                return box (stringify (createObj [ "error", box "Signal target is not an active PTY" ]))
                            | None ->
                                let sid = contextString ctx "sessionID" |> Option.defaultValue ""

                                if ToolSurfaceOrchestrator.isOrchestratorSession sessionRoles sid then
                                    if agent <> "manager" then
                                        return
                                            box (
                                                stringify (
                                                    createObj [ "error", box "Orchestrator may only fork manager jobs" ]
                                                )
                                            )
                                    else
                                        let managerId = newAgentId ()
                                        let host = orchestratorHostFor sid
                                        let! started = host.ForkManagerJob(managerId, prompt)

                                        match started with
                                        | Ok worktree ->
                                            return
                                                box (
                                                    stringify (
                                                        createObj [ "agentId", box managerId; "worktree", box worktree ]
                                                    )
                                                )
                                        | Error err -> return box (stringify (createObj [ "error", box err ]))
                                else
                                    let! result =
                                        match HostSessionContext.roleOf agent with
                                        | Some role -> runtime.Fork(newAgentId (), role, prompt)
                                        | None -> runtime.Reuse(agent, prompt)

                                    match result with
                                    | Ok fork -> return box (stringify (createObj [ "agentId", box fork.AgentId ]))
                                    | Error err -> return box (stringify (createObj [ "error", box err ]))
            }

        let joinExecute (_args: obj) (ctx: obj) =
            task {
                let sid = contextString ctx "sessionID" |> Option.defaultValue ""

                if ToolSurfaceOrchestrator.isOrchestratorSession sessionRoles sid then
                    let host = orchestratorHostFor sid
                    let! verdict = host.JoinPublished()
                    return box (stringify (createObj [ "outcome", box verdict ]))
                else
                    match runtimeFor ctx with
                    | Error err -> return box (stringify (createObj [ "error", box err ]))
                    | Ok runtime ->
                        let! result = runtime.Join()

                        match result with
                        | Ok c ->
                            let fields =
                                [ "agentId", box c.AgentId; "runId", box c.RunId; "outcome", box c.Outcome ]
                                @ (if runtime.IsPtyCompletion c.RunId then
                                       [ "ptyId", box c.RunId ]
                                   else
                                       [])

                            return box (stringify (createObj fields))
                        | Error e -> return box (stringify (createObj [ "error", box (e.ToString()) ]))
            }

        let listExecute (_args: obj) (ctx: obj) =
            task {
                match runtimeFor ctx with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let agents, ptys = runtime.List()

                    let agentEntries =
                        agents
                        |> List.sortBy (fun a -> a.AgentId)
                        |> List.map (fun a ->
                            createObj
                                [ "kind", box ListKind.Agent
                                  "agentId", box a.AgentId
                                  "role", box (a.Role.ToString())
                                  "status", box (a.Status.ToString()) ])

                    let ptyEntries =
                        ptys
                        |> List.sortBy (fun p -> p.PtyId)
                        |> List.map (fun p ->
                            createObj
                                [ "kind", box ListKind.Pty
                                  "ptyId", box p.PtyId
                                  "command", box p.Command
                                  "startedAt", box p.StartedAt ])

                    return box (stringify (box (List.append agentEntries ptyEntries |> List.toArray)))
            }

        let verdictExecute =
            VerdictSurface.create
                sessionParents
                sessionRoles
                journal
                (fun reviewerId ->
                    match worktreeTreePorts.TryGetValue reviewerId with
                    | true, port -> Some port
                    | false, _ -> gitTreePort)
                reviewerHosts
                verdictSessions

        let forkArgs, verdictArgs, definition =
            ToolSurfaceOrchestrator.toolDefBuilders factory

        let executorRuntimeFor =
            ToolSurfaceOrchestrator.executorRuntimeFor
                gate
                executorRuntimes
                mkSid
                sessionPort
                sessionParents
                sessionRoles
                modelConfig

        let executor =
            ExecutorTool.create toolModule runtimeFor executorRuntimeFor workspaceDirectory

        createObj
            [ "fork",
              box (applyTool factory (definition "Fork, nudge, or control an agent or PTY" forkArgs forkExecute))
              "join",
              box (applyTool factory (definition "Wait for any agent or PTY completion" (createObj []) joinExecute))
              "list", box (applyTool factory (definition "List active agents and PTYs" (createObj []) listExecute))
              "verdict", box (applyTool factory (definition "Submit the review verdict" verdictArgs verdictExecute))
              "executor", executor ]
