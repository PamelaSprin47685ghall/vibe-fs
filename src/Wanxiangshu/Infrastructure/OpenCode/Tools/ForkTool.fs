namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Manager fork/nudge and Orchestrator manager-job creation. Each public tool
/// has its own typed request and schema; PTY is intentionally absent.
module ForkTool =

    type Request =
        { Agent: string
          Prompt: string
          Tdd: string option }

    let private decode (args: HostToolArguments) =
        { Agent = args.Text "agent"
          Prompt = args.Text "prompt"
          Tdd = args.OptionalText "tdd" }

    let private error (message: string) =
        ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString message ]

    /// Optional tdd: absent → prompt unchanged; present → fail-closed parse then compose.
    let private childPrompt (request: Request) : Result<string, string> =
        match request.Tdd with
        | None -> Ok request.Prompt
        | Some raw ->
            match TddPhase.parseTddPhase raw with
            | Error parseError -> Error parseError
            | Ok phase -> Ok(TddPhase.composeAssignment phase request.Prompt)

    let private forkPayload (agentId: string) (managed: ManagedAgent) (extra: (string * ToolHostCodec.TomlValue) list) =
        let peer = ManagedAgent.peer managed

        ToolHostCodec.tomlObject (
            [ "agent_id", ToolHostCodec.TString agentId
              "agent", ToolHostCodec.TString managed.Name
              "role", ToolHostCodec.TString(ManagedAgent.roleName managed.Role)
              "tier", ToolHostCodec.TString(ManagedAgent.tierName managed.Tier)
              "fallback_peer", ToolHostCodec.TString peer.Name ]
            @ extra
        )

    let private unknownAgentError (raw: string) =
        match ManagedAgent.parse raw with
        | Error parseError -> ManagedAgent.formatParseError parseError
        | Ok _ -> sprintf "Unknown managed agent '%s'." raw

    let private managedForRecord (record: AgentRecord) =
        if String.IsNullOrWhiteSpace record.Agent then
            None
        else
            ManagedAgent.tryParse record.Agent

    let private forbiddenManagerRole (managed: ManagedAgent) =
        match managed.Role with
        | Role.Executor
        | Role.Blogger
        | Role.Orchestrator
        | Role.Manager -> true
        | _ -> false

    let private TddSchemaDescription =
        "Optional TDD phase. Use red to establish a failing behavior test and green to implement the smallest production change that makes the established test pass. Required by prompt when forking a coder role; omit for other roles."

    let private executeManager (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            if request.Agent = Wanxiangshu.Process.Pty.AgentName then
                return error "PTY operations require the fork-pty tool on a DevOps agent"
            elif String.IsNullOrWhiteSpace request.Agent then
                return error "agent is required; use an explicit fast-* or deep-* managed agent name"
            else
                match childPrompt request with
                | Error tddError -> return error tddError
                | Ok prompt ->
                    match scope.RuntimeFor context with
                    | Error runtimeError -> return error runtimeError
                    | Ok runtime ->
                        let retired = runtime.IsRetiredHandle request.Agent

                        let pty =
                            match retired with
                            | Some true -> None
                            | _ -> runtime.TryPty request.Agent

                        match retired, pty with
                        | Some true, _ -> return error (sprintf "RetiredHandle: %s" request.Agent)
                        | _, Some _ -> return error "PTY operations require the fork-pty tool on a DevOps agent"
                        | _, None ->
                            match runtime.TryFindAgent request.Agent with
                            | Some record ->
                                match! runtime.Reuse(request.Agent, prompt) with
                                | Error reuseError -> return error reuseError
                                | Ok result ->
                                    match managedForRecord record with
                                    | Some managed -> return forkPayload result.AgentId managed []
                                    | None ->
                                        return
                                            ToolHostCodec.tomlObject
                                                [ "agent_id", ToolHostCodec.TString result.AgentId
                                                  "agent", ToolHostCodec.TString record.Agent
                                                  "role",
                                                  ToolHostCodec.TString(record.Role.ToString().ToLowerInvariant()) ]
                            | None ->
                                match ManagedAgent.tryParse request.Agent with
                                | Some managed when forbiddenManagerRole managed ->
                                    return
                                        error (
                                            sprintf
                                                "Manager may not fork role '%s'"
                                                (ManagedAgent.roleName managed.Role)
                                        )
                                | Some managed when
                                    managed.Visibility = AgentVisibility.Public
                                    && List.contains managed.Name ManagedAgent.publicForkableNames
                                    ->
                                    let role = AgentRoleIdentity.ofManaged managed

                                    match!
                                        runtime.Fork(ToolHostCodec.newHandleId (), role, managed.Name, prompt, None)
                                    with
                                    | Ok result -> return forkPayload result.AgentId managed []
                                    | Error forkError -> return error forkError
                                | Some managed ->
                                    return
                                        error (
                                            sprintf "Managed agent '%s' is not creatable via Manager fork" managed.Name
                                        )
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
                    let managerId = ManagerJobId.create (ToolHostCodec.newHandleId ())
                    let host = scope.OrchestratorHostFor context.SessionId

                    match! host.ForkManagerJob(managerId, managed.Name, request.Prompt) with
                    | Ok worktree ->
                        return
                            forkPayload
                                (ManagerJobId.value managerId)
                                managed
                                [ "worktree", ToolHostCodec.TString worktree ]
                    | Error forkError -> return error forkError
                | Some _ -> return error "Orchestrator may only fork fast-manager or deep-manager"
                | None -> return error (unknownAgentError request.Agent)
        }

    let managerSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fork"
          Description =
            "Create a managed agent, or reuse/nudge an existing agent by passing its agent_id. Prefer reuse when the existing sub-session has compatible context. Optional tdd=red|green; required by prompt when forking a coder role."
          Arguments =
            [ "agent", ToolHostCodec.managedOrHandleSchema ManagedAgent.publicForkableNames factory
              "prompt", ToolHostCodec.optionalStringSchema factory
              "tdd", ToolHostCodec.optionalEnumSchemaDescribed [ "red"; "green" ] TddSchemaDescription factory ]
          Execute = fun args context -> executeManager scope (decode args) context }

    let orchestratorSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fork-manager"
          Description = "Fork a manager job"
          Arguments =
            [ "agent", ToolHostCodec.enumSchema ManagedAgent.orchestratorForkableNames factory
              "prompt", ToolHostCodec.stringSchema factory ]
          Execute = fun args context -> executeOrchestrator scope (decode args) context }
