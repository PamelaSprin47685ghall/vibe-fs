namespace Wanxiangshu.Next.OpenCode

open System
open Thoth.Json
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Session

/// Manager fork/nudge and Orchestrator manager-job creation. Each public tool
/// has its own typed request and schema; PTY is intentionally absent.
module ForkTool =

    type Request = { Agent: string; Prompt: string }

    let private decode (args: HostToolArguments) =
        { Agent = args.Text "agent"
          Prompt = args.Text "prompt" }

    let private error (message: string) =
        ToolHostCodec.jsonObject [ "error", Encode.string message ]

    let private forkPayload (agentId: string) (managed: ManagedAgent) extra =
        let peer = ManagedAgent.peer managed

        ToolHostCodec.jsonObject
            ([ "agentId", Encode.string agentId
               "agent", Encode.string managed.Name
               "role", Encode.string (ManagedAgent.roleName managed.Role)
               "tier", Encode.string (ManagedAgent.tierName managed.Tier)
               "fallbackPeer", Encode.string peer.Name ]
             @ extra)

    let private unknownAgentError (raw: string) =
        match ManagedAgent.parse raw with
        | Error parseError -> ManagedAgent.formatParseError parseError
        | Ok _ -> sprintf "Unknown managed agent '%s'." raw

    let private managedForRecord (record: AgentRecord) =
        if String.IsNullOrWhiteSpace record.Agent then None else ManagedAgent.tryParse record.Agent

    let private forbiddenManagerRole (managed: ManagedAgent) =
        match managed.Role with
        | Role.Executor
        | Role.Blogger
        | Role.Orchestrator
        | Role.Manager -> true
        | _ -> false

    let private executeManager (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            if request.Agent = Wanxiangshu.Next.Process.Pty.AgentName then
                return error "PTY operations require the fork-pty tool on a DevOps agent"
            elif String.IsNullOrWhiteSpace request.Agent then
                return error "agent is required; use an explicit fast-* or deep-* managed agent name"
            else
                match scope.RuntimeFor context with
                | Error runtimeError -> return error runtimeError
                | Ok runtime ->
                    match runtime.TryPty request.Agent with
                    | Some _ -> return error "PTY operations require the fork-pty tool on a DevOps agent"
                    | None ->
                        match runtime.TryFindAgent request.Agent with
                        | Some record when record.Status = AgentStatus.Closed ->
                            return error (sprintf "Retired agent handle '%s' cannot be reused" request.Agent)
                        | Some record ->
                            match! runtime.Reuse(request.Agent, request.Prompt) with
                            | Error reuseError -> return error reuseError
                            | Ok result ->
                                match managedForRecord record with
                                | Some managed -> return forkPayload result.AgentId managed []
                                | None ->
                                    return
                                        ToolHostCodec.jsonObject
                                            [ "agentId", Encode.string result.AgentId
                                              "agent", Encode.string record.Agent
                                              "role", Encode.string (record.Role.ToString().ToLowerInvariant()) ]
                        | None ->
                            match ManagedAgent.tryParse request.Agent with
                            | Some managed when forbiddenManagerRole managed ->
                                return
                                    error
                                        (sprintf
                                            "Manager may not fork role '%s'"
                                            (ManagedAgent.roleName managed.Role))
                            | Some managed when
                                managed.Visibility = AgentVisibility.Public
                                && List.contains managed.Name ManagedAgent.publicForkableNames
                                ->
                                let role = AgentRoleIdentity.ofManaged managed

                                match!
                                    runtime.Fork(
                                        ToolHostCodec.newHandleId (),
                                        role,
                                        request.Prompt,
                                        agent = managed.Name
                                    )
                                with
                                | Ok result -> return forkPayload result.AgentId managed []
                                | Error forkError -> return error forkError
                            | Some managed ->
                                return
                                    error
                                        (sprintf
                                            "Managed agent '%s' is not creatable via Manager fork"
                                            managed.Name)
                            | None when ToolHostCodec.looksLikeHandleId request.Agent ->
                                return error (sprintf "Unknown agent id: %s" request.Agent)
                            | None -> return error (unknownAgentError request.Agent)
        }

    let private executeOrchestrator (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            if String.IsNullOrWhiteSpace context.SessionId then
                return error "Missing sessionID"
            else
                match ManagedAgent.tryParse request.Agent with
                | Some managed when managed.Role = Role.Manager && managed.Visibility = AgentVisibility.Public ->
                    let managerId = ToolHostCodec.newHandleId ()
                    let host = scope.OrchestratorHostFor context.SessionId

                    match! host.ForkManagerJob(managerId, managed.Name, request.Prompt) with
                    | Ok worktree -> return forkPayload managerId managed [ "worktree", Encode.string worktree ]
                    | Error forkError -> return error forkError
                | Some _ -> return error "Orchestrator may only fork fast-manager or deep-manager"
                | None -> return error (unknownAgentError request.Agent)
        }

    let managerSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fork"
          Description = "Fork or nudge an agent"
          Arguments =
            [ "agent", ToolHostCodec.managedOrHandleSchema ManagedAgent.publicForkableNames factory
              "prompt", ToolHostCodec.optionalStringSchema factory ]
          Execute = fun args context -> executeManager scope (decode args) context }

    let orchestratorSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fork-manager"
          Description = "Fork a manager job"
          Arguments =
            [ "agent", ToolHostCodec.enumSchema ManagedAgent.orchestratorForkableNames factory
              "prompt", ToolHostCodec.stringSchema factory ]
          Execute = fun args context -> executeOrchestrator scope (decode args) context }
