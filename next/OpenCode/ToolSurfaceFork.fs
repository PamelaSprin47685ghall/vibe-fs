namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open Fable.Core.JsInterop
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session
open ToolSurfaceEmit
open ToolSurfacePty

/// Manager/Orchestrator agent fork surface. PTY is not reachable here.
module ToolSurfaceFork =

    let forkExecute (deps: PtyToolDeps) (args: obj) (ctx: obj) =
        task {
            let agent = textArg args ToolSurfaceFields.ForkField.Agent
            let prompt = textArg args ToolSurfaceFields.ForkField.Prompt
            let sid = sessionIdOf ctx

            if agent = Pty.AgentName then
                return
                    box (
                        stringify (
                            createObj
                                [ "error",
                                  box "PTY operations require the fork-pty tool on a DevOps agent" ]
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
                                        [ "error",
                                          box "PTY operations require the fork-pty tool on a DevOps agent" ]
                                )
                            )
                    | None ->
                        let effectiveAgent =
                            if String.IsNullOrWhiteSpace agent
                               && ToolSurfaceOrchestrator.isOrchestratorSession deps.SessionRoles sid then
                                "manager"
                            else
                                agent

                        if ToolSurfaceOrchestrator.isOrchestratorSession deps.SessionRoles sid then
                            if effectiveAgent <> "manager" then
                                return
                                    box (
                                        stringify (
                                            createObj [ "error", box "Orchestrator may only fork manager jobs" ]
                                        )
                                    )
                            else
                                let managerId = newAgentId ()
                                let host = deps.OrchestratorHostFor sid
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
                            match HostSessionContext.roleOf agent with
                            | Some AgentRole.Executor
                            | Some AgentRole.Blogger
                            | Some AgentRole.Orchestrator
                            | Some AgentRole.Manager ->
                                return
                                    box (
                                        stringify (
                                            createObj
                                                [ "error",
                                                  box (
                                                      sprintf
                                                          "Manager may not fork role '%s'"
                                                          (agent.ToLowerInvariant())
                                                  ) ]
                                        )
                                    )
                            | Some role ->
                                let! result = runtime.Fork(newAgentId (), role, prompt)

                                match result with
                                | Ok fork -> return box (stringify (createObj [ "agentId", box fork.AgentId ]))
                                | Error err -> return box (stringify (createObj [ "error", box err ]))
                            | None ->
                                let! result = runtime.Reuse(agent, prompt)

                                match result with
                                | Ok fork -> return box (stringify (createObj [ "agentId", box fork.AgentId ]))
                                | Error err -> return box (stringify (createObj [ "error", box err ]))
        }
