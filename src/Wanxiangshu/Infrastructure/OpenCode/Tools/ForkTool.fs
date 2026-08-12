namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Manager fork / Orchestrator commission. Each public tool has its own typed
/// request and schema; PTY is intentionally absent.
module ForkTool =

    type Request =
        { Calling: string
          Name: string
          Charge: string
          Keywords: string }

    let private decode (args: HostToolArguments) =
        { Calling = args.Text "calling"
          Name = args.Text "name"
          Charge = args.Text "charge"
          Keywords = args.Text "keywords" }

    let private consequence (message: string) =
        ToolHostCodec.tomlObjectWithInstructions [ message ] []

    let private successInstruction (text: string) =
        ToolHostCodec.tomlObjectWithInstructions [ text ] []

    let private unknownCallingConsequence () =
        "Unknown or unavailable calling."

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

    let private personaBinding (role: Role) (tier: AgentTier) =
        PersonaCatalog.persona role tier |> fun value -> value.ToLowerInvariant(), ManagedAgent.make tier role

    let private managerCallingBindings =
        [ for role in ManagedAgentCatalog.managerForkableRoles do
              yield personaBinding role AgentTier.Fast
              yield personaBinding role AgentTier.Deep ]

    let private orchestratorCallingBindings =
        [ personaBinding Role.Manager AgentTier.Fast
          personaBinding Role.Manager AgentTier.Deep ]

    let private callingNames bindings = bindings |> List.map fst

    let private tryCalling bindings (raw: string) =
        if String.IsNullOrWhiteSpace raw then
            None
        else
            let wanted = raw.Trim().ToLowerInvariant()
            bindings |> List.tryPick (fun (name, managed) -> if name = wanted then Some managed else None)

    let private hasCalling (request: Request) = not (String.IsNullOrWhiteSpace request.Calling)

    let private hasKeywords (request: Request) =
        not (String.IsNullOrWhiteSpace request.Keywords)

    let private warmStartAllowed role =
        RepositoryWarmStartPrompt.isDirectConsumer role

    let private warmStartError =
        "repository warm-start keywords are only available when fork targets Coder, Inspector, or DevOps"

    let private prepareForkPrompt (scope: ToolRuntimeScope) (runtime: HostForkRuntime) (role: Role) (request: Request) =
        task {
            let basePrompt =
                ForkChildPayload.relay request.Charge (runtime.ParentWorkRecordOf runtime.ParentId) [] None

            match! RepositoryWarmStart.appendToBase role scope.WorkspaceDirectory request.Keywords basePrompt with
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
            if String.IsNullOrWhiteSpace request.Name then
                return consequence "A name is required."
            elif String.IsNullOrWhiteSpace request.Charge then
                return consequence "A charge is required."
            else
                match scope.RuntimeFor context with
                | Error _ -> return consequence "A charge cannot be placed from this execution context."
                | Ok runtime ->
                    let handles =
                        match scope.Journal with
                        | Some journal when not (String.IsNullOrWhiteSpace context.SessionId) ->
                            Some(AgentJournal.handleProjection journal (SessionId.create context.SessionId))
                        | _ -> None

                    let existingByname =
                        handles |> Option.bind (HandleProjection.tryFindByByname request.Name)

                    if hasCalling request then
                        match existingByname with
                        | Some _ ->
                            return consequence "That name already belongs to someone in this continuing history."
                        | None ->
                            match tryCalling managerCallingBindings request.Calling with
                            | None -> return consequence (unknownCallingConsequence ())
                            | Some managed ->
                                let role = AgentRoleIdentity.ofManaged managed.Role

                                if hasKeywords request && not (warmStartAllowed role) then
                                    return consequence warmStartError
                                else
                                    let handleId = ToolHostCodec.newHandleId ()

                                    let! forkResult =
                                        if hasKeywords request then
                                            task {
                                                let! rendered = prepareForkPrompt scope runtime role request

                                                return!
                                                    runtime.Fork(
                                                        handleId,
                                                        role,
                                                        managed.Name,
                                                        request.Charge,
                                                        None,
                                                        renderedPrompt = rendered,
                                                        byname = request.Name
                                                    )
                                            }
                                        else
                                            runtime.Fork(
                                                handleId,
                                                role,
                                                managed.Name,
                                                request.Charge,
                                                None,
                                                byname = request.Name
                                            )

                                    match forkResult with
                                    | Ok _ ->
                                        return successInstruction (sprintf "%s carries this charge now." (request.Name.Trim()))
                                    | Error _ -> return consequence "The charge could not be placed."
                    else
                        match existingByname with
                        | None -> return consequence "No continuing person is known by that name."
                        | Some handle ->
                            match HandleId.tryAgent handle.Handle with
                            | None -> return consequence "No continuing person is known by that name."
                            | Some handleId ->
                                let agentId = AgentHandleId.value handleId

                                match runtime.TryFindAgent agentId with
                                | None -> return consequence "That person is not presently available for another charge."
                                | Some record when hasKeywords request && not (warmStartAllowed record.Role) ->
                                    return consequence warmStartError
                                | Some record ->
                                    let activeRun =
                                        lock runtime.Gate (fun () -> runtime.PendingRuns.ContainsKey agentId)

                                    let! reuseResult =
                                        if hasKeywords request && not activeRun then
                                            task {
                                                let! rendered = prepareForkPrompt scope runtime record.Role request
                                                return! runtime.Reuse(agentId, request.Charge, renderedPrompt = rendered)
                                            }
                                        else
                                            runtime.Reuse(agentId, request.Charge)

                                    match reuseResult with
                                    | Error _ -> return consequence "That person cannot take another charge yet."
                                    | Ok _ ->
                                        return successInstruction (sprintf "%s carries this charge now." (request.Name.Trim()))
        }

    let private executeOrchestrator (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            if String.IsNullOrWhiteSpace context.SessionId then
                return consequence "A road cannot be commissioned before the caller's authority is established."
            elif String.IsNullOrWhiteSpace request.Name then
                return consequence "A name is required."
            elif String.IsNullOrWhiteSpace request.Charge then
                return consequence "A charge is required."
            else
                let existingByname =
                    scope.Journal
                    |> Option.bind (fun journal ->
                        (AgentJournal.snapshot journal).AgentProjections.Orchestrator
                        |> OrchestratorProjection.tryFindByByname request.Name)

                if hasCalling request then
                    match existingByname with
                    | Some _ -> return consequence "That name already belongs to a road in this continuing history."
                    | None ->
                        match tryCalling orchestratorCallingBindings request.Calling with
                        | None -> return consequence (unknownCallingConsequence ())
                        | Some managed ->
                            let managerId = ManagerJobId.create (ToolHostCodec.newHandleId ())
                            let host = scope.OrchestratorHostFor context.SessionId

                            match!
                                host.ForkManagerJob(
                                    managerId,
                                    managed.Name,
                                    request.Charge,
                                    byname = request.Name
                                )
                            with
                            | Ok _ ->
                                return successInstruction (sprintf "%s has taken your charge." (request.Name.Trim()))
                            | Error _ -> return consequence "That road could not be opened."
                else
                    match existingByname with
                    | None -> return consequence "No continuing road is known by that name."
                    | Some job ->
                        let host = scope.OrchestratorHostFor context.SessionId

                        match! host.ContinueManagerJob(job.ManagerJobId, request.Charge) with
                        | Ok _ ->
                            return successInstruction (sprintf "%s has taken your charge." (request.Name.Trim()))
                        | Error _ -> return consequence "That road cannot take another charge."
        }

    let managerSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fork"
          Description =
            "Commission another witness within a mission. For a new person pass calling + name + charge; to continue someone already known here, omit calling and use the same name."
          Arguments =
            [ "calling", ToolHostCodec.optionalEnumSchema (callingNames managerCallingBindings) factory
              "name", ToolHostCodec.stringSchema factory
              "charge", ToolHostCodec.stringSchema factory
              "keywords", ToolHostCodec.optionalStringSchema factory ]
          Execute = fun args context -> executeManager scope (decode args) context }

    let orchestratorSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "commission"
          Description =
            "Entrust an independent road to a Manager. For a new road pass calling + name + charge; to continue a known road, omit calling and use the same name."
          Arguments =
            [ "calling", ToolHostCodec.optionalEnumSchema (callingNames orchestratorCallingBindings) factory
              "name", ToolHostCodec.stringSchema factory
              "charge", ToolHostCodec.stringSchema factory ]
          Execute = fun args context -> executeOrchestrator scope (decode args) context }
