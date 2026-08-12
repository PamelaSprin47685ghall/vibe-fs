namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Manager fork / Orchestrator commission. Each public tool has its own typed
/// request and schema; PTY is intentionally absent.
module ForkTool =

    type Request =
        { Name: string
          Charge: string
          Keywords: string }

    let private decode (args: HostToolArguments) =
        { Name = args.Text "name"
          Charge = args.Text "charge"
          Keywords = args.Text "keywords" }

    let private error (message: string) =
        ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString message ]

    let private successInstruction (text: string) =
        ToolHostCodec.tomlObjectWithInstructions [ text ] []

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
        | Role.Distiller
        | Role.Blogger
        | Role.Orchestrator
        | Role.Manager
        | Role.Reviewer -> true
        | _ -> false

    let private hasKeywords (request: Request) = not (String.IsNullOrWhiteSpace request.Keywords)

    let private warmStartAllowed role = RepositoryWarmStartPrompt.isDirectConsumer role

    let private warmStartError =
        "repository warm-start keywords are only available when fork targets Coder, Inspector, or DevOps"

    let private prepareForkPrompt
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (role: Role)
        (request: Request)
        =
        task {
            let basePrompt =
                ForkChildPayload.relay
                    request.Charge
                    (runtime.ParentWorkRecordOf runtime.ParentId)
                    []
                    None

            match!
                RepositoryWarmStart.appendToBase role scope.WorkspaceDirectory request.Keywords basePrompt
            with
            | Ok prompt -> return prompt
            | Error _ -> return basePrompt
        }

    let private bynameOf (request: Request) (fallback: string) =
        if String.IsNullOrWhiteSpace request.Name then
            fallback
        else
            request.Name.Trim()

    let private executeManager (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            if request.Name = Wanxiangshu.Process.Pty.AgentName then
                return
                    error
                        "PTY operations require the open-terminal / send-terminal / read-terminal / signal-terminal tools on a DevOps agent"
            elif String.IsNullOrWhiteSpace request.Name then
                return error "name is required"
            elif String.IsNullOrWhiteSpace request.Charge then
                return error "charge is required"
            else
                let assignment = request.Charge

                match scope.RuntimeFor context with
                | Error runtimeError -> return error runtimeError
                | Ok runtime ->
                    let abandoned =
                        match runtime.IsRetiredHandle request.Name with
                        | Some true -> true
                        | _ -> false

                    let pty =
                        match abandoned with
                        | true -> None
                        | false -> runtime.TryPty request.Name

                    match abandoned, pty, runtime.TryFindAgent request.Name with
                    | true, _, None -> return error (sprintf "RetiredHandle: %s" request.Name)
                    | _, Some _, _ ->
                        return
                            error
                                "PTY operations require the open-terminal / send-terminal / read-terminal / signal-terminal tools on a DevOps agent"
                    | _, None, Some record ->
                        match managedForRecord record with
                        | Some managed when forbiddenManagerRole managed -> return error HiddenTargetDeniedText
                        | _ when hasKeywords request && not (warmStartAllowed record.Role) ->
                            return error warmStartError
                        | _ ->
                            let activeRun =
                                lock runtime.Gate (fun () -> runtime.PendingRuns.ContainsKey request.Name)

                            let! reuseResult =
                                if hasKeywords request && not activeRun then
                                    task {
                                        let! rendered = prepareForkPrompt scope runtime record.Role request
                                        return! runtime.Reuse(request.Name, assignment, renderedPrompt = rendered)
                                    }
                                else
                                    runtime.Reuse(request.Name, assignment)

                            match reuseResult with
                            | Error reuseError -> return error reuseError
                            | Ok _ ->
                                let label =
                                    match managedForRecord record with
                                    | Some managed -> managed.Name
                                    | None -> record.Agent

                                return successInstruction (sprintf "# %s carries this charge now." label)
                    | _, None, None ->
                        match ManagedAgent.tryParse request.Name with
                        | Some managed when forbiddenManagerRole managed -> return error HiddenTargetDeniedText
                        | Some managed when
                            managed.Visibility = AgentVisibility.Public
                            && List.contains managed.Name ManagedAgent.managerForkableNames
                            ->
                            let role = AgentRoleIdentity.ofManaged managed.Role

                            if hasKeywords request && not (warmStartAllowed role) then
                                return error warmStartError
                            else
                                let! forkResult =
                                    if hasKeywords request then
                                        task {
                                            let! rendered = prepareForkPrompt scope runtime role request

                                            return!
                                                runtime.Fork(
                                                    ToolHostCodec.newHandleId (),
                                                    role,
                                                    managed.Name,
                                                    assignment,
                                                    None,
                                                    renderedPrompt = rendered
                                                )
                                        }
                                    else
                                        runtime.Fork(ToolHostCodec.newHandleId (), role, managed.Name, assignment, None)

                                match forkResult with
                                | Ok _ ->
                                    return
                                        successInstruction (
                                            sprintf "# %s carries this charge now." (bynameOf request managed.Name)
                                        )
                                | Error forkError -> return error forkError
                        | Some managed when forbiddenManagerRole managed -> return error HiddenTargetDeniedText
                        | Some managed ->
                            return error (sprintf "Managed agent '%s' is not creatable via Manager fork" managed.Name)
                        | None when ToolHostCodec.looksLikeHandleId request.Name ->
                            return error (sprintf "Unknown agent id: %s" request.Name)
                        | None -> return error (unknownAgentError request.Name)
        }

    let private executeOrchestrator (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            if String.IsNullOrWhiteSpace context.SessionId then
                return error "Missing sessionID"
            elif String.IsNullOrWhiteSpace request.Name then
                return error "name is required"
            elif String.IsNullOrWhiteSpace request.Charge then
                return error "charge is required"
            else
                match ManagedAgent.tryParse request.Name with
                | Some managed when managed.Role = Role.Manager && managed.Visibility = AgentVisibility.Public ->
                    let managerId = ManagerJobId.create (ToolHostCodec.newHandleId ())
                    let host = scope.OrchestratorHostFor context.SessionId

                    match! host.ForkManagerJob(managerId, managed.Name, request.Charge) with
                    | Ok _ ->
                        return
                            successInstruction (sprintf "# %s has taken your charge." (bynameOf request managed.Name))
                    | Error forkError -> return error forkError
                | Some _ -> return error "Orchestrator may only commission fast-manager or deep-manager"
                | None ->
                    // GLORY-068: reuse an existing ManagerJob — same worktree/session.
                    if ToolHostCodec.looksLikeHandleId request.Name then
                        let host = scope.OrchestratorHostFor context.SessionId
                        let jobId = ManagerJobId.create request.Name

                        match! host.ContinueManagerJob(jobId, request.Charge) with
                        | Ok _ ->
                            return
                                successInstruction (
                                    sprintf "# %s has taken your charge." (bynameOf request request.Name)
                                )
                        | Error reuseError -> return error reuseError
                    else
                        return error (unknownAgentError request.Name)
        }

    let managerSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fork"
          Description =
            "Commission another witness within a mission. Pass name + charge; reuse by the same name when the existing sub-session has compatible context."
          Arguments =
            [ "name", ToolHostCodec.managedOrHandleSchema ManagedAgent.managerForkableNames factory
              "charge", ToolHostCodec.optionalStringSchema factory
              "keywords", ToolHostCodec.optionalStringSchema factory ]
          Execute = fun args context -> executeManager scope (decode args) context }

    let orchestratorSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "commission"
          Description =
            "Entrust an independent road to a Manager. Pass name + charge; reuse an existing road by passing its handle as name."
          Arguments =
            [ "name", ToolHostCodec.managedOrHandleSchema ManagedAgent.orchestratorForkableNames factory
              "charge", ToolHostCodec.stringSchema factory ]
          Execute = fun args context -> executeOrchestrator scope (decode args) context }
