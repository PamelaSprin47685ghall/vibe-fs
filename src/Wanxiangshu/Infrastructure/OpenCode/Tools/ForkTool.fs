namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
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

    /// Optional tdd: absent → prompt unchanged + no phase; present → fail-closed parse, then
    /// compose the phase constraint into the assignment text (reused by Reuse/nudge paths) and
    /// retain the typed phase for first-prompt `ForkChildPayload.render`.
    let private childPrompt (request: Request) : Result<string * TddPhase option, string> =
        match request.Tdd with
        | None -> Ok(request.Prompt, None)
        | Some raw ->
            match TddPhase.parseTddPhase raw with
            | Error parseError -> Error parseError
            | Ok phase -> Ok(TddPhase.composeAssignment phase request.Prompt, Some phase)

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

    /// GLORY-032: provider-facing denial for any target the Manager cannot
    /// reach (the Host-owned Reviewer among them). Generic — it must not prove
    /// the hidden target exists.
    let HiddenTargetDeniedText = "Unknown or unavailable managed agent."

    let private forbiddenManagerRole (managed: ManagedAgent) =
        match managed.Role with
        | Role.Executor
        | Role.Blogger
        | Role.Orchestrator
        | Role.Manager
        | Role.Reviewer -> true
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
                | Ok(assignment, tdd) ->
                    match scope.RuntimeFor context with
                    | Error runtimeError -> return error runtimeError
                    | Ok runtime ->
                        // IsRetiredHandle is true for both Abandoned and join-Retired.
                        // Only Abandoned is terminal; a join-Retired handle may be
                        // reopened by Reuse on the same child session.
                        let abandoned =
                            match runtime.IsRetiredHandle request.Agent with
                            | Some true ->
                                // Distinguish Abandoned from join-Retired via journal
                                // projection when available; treat true as "blocked"
                                // only when TryFindAgent still cannot open Reuse.
                                true
                            | _ -> false

                        let pty =
                            match abandoned with
                            | true -> None
                            | false -> runtime.TryPty request.Agent

                        match abandoned, pty, runtime.TryFindAgent request.Agent with
                        | true, _, None -> return error (sprintf "RetiredHandle: %s" request.Agent)
                        | _, Some _, _ -> return error "PTY operations require the fork-pty tool on a DevOps agent"
                        | _, None, Some record ->
                            // GLORY-031/032: reuse of a hidden target (the
                            // Host-owned Reviewer among them) is denied by its
                            // durable role, before any nudge is sent.
                            match managedForRecord record with
                            | Some managed when forbiddenManagerRole managed -> return error HiddenTargetDeniedText
                            | _ ->
                                // Reuse / busy nudge / post-join reopen.
                                match! runtime.Reuse(request.Agent, assignment) with
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
                        | _, None, None ->
                            match ManagedAgent.tryParse request.Agent with
                            | Some managed when forbiddenManagerRole managed -> return error HiddenTargetDeniedText
                            | Some managed when
                                managed.Visibility = AgentVisibility.Public
                                && List.contains managed.Name ManagedAgent.managerForkableNames
                                ->
                                let role = AgentRoleIdentity.ofManaged managed.Role
                                // PENDING 7: the Manager fork owns the first-prompt payload for
                                // Coder children. When `tdd` is present, render the full ARCH-010
                                // document here so the durable `[tdd]` table reaches the child wire;
                                // `assignment` (the composed TDD text) stays the record's Assignment
                                // field. HostForkAgent keeps session creation, opening capture and the
                                // review barrier, and sends this rendered document verbatim via
                                // `renderedPrompt`. Without a phase the Host's own relay envelope is
                                // used, byte-identical to the pre-PENDING-7 shape.
                                let renderedPrompt =
                                    tdd
                                    |> Option.map (fun _ ->
                                        ForkChildPayload.render
                                            { Assignment = assignment
                                              ParentWorkRecord = scope.ParentWorkRecordFor context.SessionId
                                              OriginalUserRequirements = []
                                              Payload = None
                                              TddPhase = tdd })

                                match!
                                    runtime.Fork(
                                        ToolHostCodec.newHandleId (),
                                        role,
                                        managed.Name,
                                        assignment,
                                        None,
                                        ?renderedPrompt = renderedPrompt
                                    )
                                with
                                | Ok result -> return forkPayload result.AgentId managed []
                                | Error forkError -> return error forkError
                            | Some managed when forbiddenManagerRole managed -> return error HiddenTargetDeniedText
                            | Some managed ->
                                return
                                    error (sprintf "Managed agent '%s' is not creatable via Manager fork" managed.Name)
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
                | None ->
                    // GLORY-068: reuse an existing ManagerJob — the same worktree
                    // and session continue with the appended requirement.
                    if ToolHostCodec.looksLikeHandleId request.Agent then
                        let host = scope.OrchestratorHostFor context.SessionId
                        let jobId = ManagerJobId.create request.Agent

                        match! host.ContinueManagerJob(jobId, request.Prompt) with
                        | Ok worktree ->
                            return
                                ToolHostCodec.tomlObject
                                    [ "agent_id", ToolHostCodec.TString request.Agent
                                      "agent", ToolHostCodec.TString "fast-manager"
                                      "role", ToolHostCodec.TString "manager"
                                      "worktree", ToolHostCodec.TString worktree
                                      "reused", ToolHostCodec.TString "true" ]
                        | Error reuseError -> return error reuseError
                    else
                        return error (unknownAgentError request.Agent)
        }

    let managerSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fork"
          Description =
            "Create a managed agent, or reuse/nudge an existing agent by passing its agent_id. Prefer reuse when the existing sub-session has compatible context. Optional tdd=red|green; required by prompt when forking a coder role."
          Arguments =
            [ "agent", ToolHostCodec.managedOrHandleSchema ManagedAgent.managerForkableNames factory
              "prompt", ToolHostCodec.optionalStringSchema factory
              "tdd", ToolHostCodec.optionalEnumSchemaDescribed [ "red"; "green" ] TddSchemaDescription factory ]
          Execute = fun args context -> executeManager scope (decode args) context }

    let orchestratorSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fork-manager"
          Description =
            "Fork a manager job, or reuse an existing manager job by passing its job id to append a requirement."
          Arguments =
            [ "agent", ToolHostCodec.managedOrHandleSchema ManagedAgent.orchestratorForkableNames factory
              "prompt", ToolHostCodec.stringSchema factory ]
          Execute = fun args context -> executeOrchestrator scope (decode args) context }
