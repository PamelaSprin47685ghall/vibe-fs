namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Session.AgentRoleHelpers
open ToolSurfaceEmit
open ToolSurfacePty

/// Manager/Orchestrator agent fork surface. PTY is not reachable here.
module ToolSurfaceFork =

    let private unknownAgentError (raw: string) =
        match ManagedAgent.parse raw with
        | Error err -> ManagedAgent.formatParseError err
        | Ok _ -> sprintf "Unknown managed agent '%s'." raw

    let private resolveManagedForHandle (record: AgentRecord) : ManagedAgent option =
        if String.IsNullOrWhiteSpace record.Agent then
            None
        else
            ManagedAgent.tryParse record.Agent

    let private forbiddenManagerCreate (managed: ManagedAgent) =
        match managed.Role with
        | Role.Executor
        | Role.Blogger
        | Role.Orchestrator
        | Role.Manager -> true
        | _ -> false

    let forkExecute (deps: PtyToolDeps) (args: obj) (ctx: obj) =
        task {
            let agent = textArg args ToolSurfaceFields.ForkField.Agent
            let prompt = textArg args ToolSurfaceFields.ForkField.Prompt
            let sid = sessionIdOf ctx

            if agent = Pty.AgentName then
                return
                    box (
                        stringify (
                            createObj [ "error", box "PTY operations require the fork-pty tool on a DevOps agent" ]
                        )
                    )
            elif String.IsNullOrWhiteSpace agent then
                return
                    box (
                        stringify (
                            createObj
                                [ "error", box "agent is required; use an explicit fast-* or deep-* managed agent name" ]
                        )
                    )
            else
                match deps.RuntimeFor ctx with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    match runtime.TryPty agent with
                    | Some _ ->
                        return
                            box (
                                stringify (
                                    createObj
                                        [ "error", box "PTY operations require the fork-pty tool on a DevOps agent" ]
                                )
                            )
                    | None ->
                        if ToolSurfaceOrchestrator.isOrchestratorSession deps.SessionRoles sid then
                            match ManagedAgent.tryParse agent with
                            | Some managed when
                                managed.Role = Role.Manager && managed.Visibility = AgentVisibility.Public
                                ->
                                let managerId = newAgentId ()
                                let host = deps.OrchestratorHostFor sid
                                let! started = host.ForkManagerJob(managerId, managed.Name, prompt)

                                match started with
                                | Ok worktree ->
                                    let payload = forkResultPayload managerId managed

                                    payload?worktree <- worktree
                                    return box (stringify payload)
                                | Error err -> return box (stringify (createObj [ "error", box err ]))
                            | Some _ ->
                                return
                                    box (
                                        stringify (
                                            createObj
                                                [ "error", box "Orchestrator may only fork fast-manager or deep-manager" ]
                                        )
                                    )
                            | None -> return box (stringify (createObj [ "error", box (unknownAgentError agent) ]))
                        else
                            // Parse order: existing handle → retired handle → managed agent → handle-like → UnknownAgent
                            match runtime.TryFindAgent agent with
                            | Some record when record.Status = AgentStatus.Closed ->
                                return
                                    box (
                                        stringify (
                                            createObj
                                                [ "error",
                                                  box (sprintf "Retired agent handle '%s' cannot be reused" agent) ]
                                        )
                                    )
                            | Some record ->
                                let! result = runtime.Reuse(agent, prompt)

                                match result with
                                | Ok fork ->
                                    match resolveManagedForHandle record with
                                    | Some managed -> return box (stringify (forkResultPayload fork.AgentId managed))
                                    | None ->
                                        return
                                            box (
                                                stringify (
                                                    createObj
                                                        [ "agentId", box fork.AgentId
                                                          "agent", box record.Agent
                                                          "role", box (record.Role.ToString().ToLowerInvariant()) ]
                                                )
                                            )
                                | Error err -> return box (stringify (createObj [ "error", box err ]))
                            | None ->
                                match ManagedAgent.tryParse agent with
                                | Some managed when forbiddenManagerCreate managed ->
                                    return
                                        box (
                                            stringify (
                                                createObj
                                                    [ "error",
                                                      box (
                                                          sprintf
                                                              "Manager may not fork role '%s'"
                                                              (ManagedAgent.roleName managed.Role)
                                                      ) ]
                                            )
                                        )
                                | Some managed when
                                    managed.Visibility = AgentVisibility.Public
                                    && List.contains managed.Name ManagedAgent.publicForkableNames
                                    ->
                                    let role = ofManaged managed
                                    let! result = runtime.Fork(newAgentId (), role, prompt, agent = managed.Name)

                                    match result with
                                    | Ok fork -> return box (stringify (forkResultPayload fork.AgentId managed))
                                    | Error err -> return box (stringify (createObj [ "error", box err ]))
                                | Some managed ->
                                    return
                                        box (
                                            stringify (
                                                createObj
                                                    [ "error",
                                                      box (
                                                          sprintf
                                                              "Managed agent '%s' is not creatable via Manager fork"
                                                              managed.Name
                                                      ) ]
                                            )
                                        )
                                | None when looksLikeHandleId agent ->
                                    return
                                        box (
                                            stringify (
                                                createObj [ "error", box (sprintf "Unknown agent id: %s" agent) ]
                                            )
                                        )
                                | None -> return box (stringify (createObj [ "error", box (unknownAgentError agent) ]))
        }
