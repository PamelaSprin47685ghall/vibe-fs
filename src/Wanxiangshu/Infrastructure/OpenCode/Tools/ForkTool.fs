namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Manager fork / Orchestrator commission. Each public tool has its own typed
/// request and schema; PTY is intentionally absent.
module ForkTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<RequireQualifiedAccess>]
        module Fork =
            [<Literal>]
            let Description = "tool/fork/description"

            [<Literal>]
            let ArgCalling = "tool/fork/arg-calling"

            [<Literal>]
            let ArgName = "tool/fork/arg-name"

            [<Literal>]
            let ArgCharge = "tool/fork/arg-charge"

            [<Literal>]
            let ArgKeywords = "tool/fork/arg-keywords"

            [<Literal>]
            let NameRequired = "tool/fork/name-required"

            [<Literal>]
            let ChargeRequired = "tool/fork/charge-required"

            [<Literal>]
            let UnknownCalling = "tool/fork/unknown-calling"

            [<Literal>]
            let HiddenTargetDenied = "tool/fork/hidden-target-denied"

            [<Literal>]
            let ChargeContextUnavailable = "tool/fork/charge-context-unavailable"

            [<Literal>]
            let NameAlreadyBelongs = "tool/fork/name-already-belongs"

            [<Literal>]
            let WarmStartUnavailable = "tool/fork/warm-start-unavailable"

            [<Literal>]
            let ChargeCarried = "tool/fork/charge-carried"

            [<Literal>]
            let ChargeNotPlaced = "tool/fork/charge-not-placed"

            [<Literal>]
            let PersonUnknown = "tool/fork/person-unknown"

            [<Literal>]
            let PersonUnavailable = "tool/fork/person-unavailable"

            [<Literal>]
            let PersonCannotTakeCharge = "tool/fork/person-cannot-take-charge"

        [<RequireQualifiedAccess>]
        module Commission =
            [<Literal>]
            let Description = "tool/commission/description"

            [<Literal>]
            let ArgCalling = "tool/commission/arg-calling"

            [<Literal>]
            let ArgName = "tool/commission/arg-name"

            [<Literal>]
            let ArgCharge = "tool/commission/arg-charge"

            [<Literal>]
            let AuthorityRequired = "tool/commission/authority-required"

            [<Literal>]
            let NameRequired = "tool/commission/name-required"

            [<Literal>]
            let ChargeRequired = "tool/commission/charge-required"

            [<Literal>]
            let UnknownCalling = "tool/commission/unknown-calling"

            [<Literal>]
            let NameAlreadyBelongs = "tool/commission/name-already-belongs"

            [<Literal>]
            let ChargeTaken = "tool/commission/charge-taken"

            [<Literal>]
            let RoadNotOpened = "tool/commission/road-not-opened"

            [<Literal>]
            let RoadUnknown = "tool/commission/road-unknown"

            [<Literal>]
            let RoadCannotTakeCharge = "tool/commission/road-cannot-take-charge"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private prose language path =
        ProviderProse.render language path Map.empty

    let private namedProse language path byname =
        ProviderProse.render language path (Map [ "name", byname ])

    let private forkInstructions (sessionId: SessionId) : ForkChildInstructions =
        let lang = ProviderProse.languageOf sessionId

        { Base = ProviderProse.instructionLines lang ForkChildPayload.BasePath Map.empty
          CommissionerRecord = ProviderProse.render lang ForkChildPayload.CommissionerRecordPath Map.empty
          Requirements = ProviderProse.render lang ForkChildPayload.RequirementsPath Map.empty }

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

    let private managedForRecord (record: AgentRecord) =
        if String.IsNullOrWhiteSpace record.Agent then
            None
        else
            ManagedAgent.tryParse record.Agent

    /// GLORY-032: provider-facing denial for any target the Manager cannot
    /// reach (the Host-owned Reviewer among them). Generic — it must not prove
    /// the hidden target exists.
    let HiddenTargetDeniedText language =
        prose language Path.Fork.HiddenTargetDenied

    let private forbiddenManagerRole (managed: ManagedAgent) =
        match managed.Role with
        | Role.Distiller
        | Role.Blogger
        | Role.Orchestrator
        | Role.Manager
        | Role.Reviewer -> true
        | _ -> false

    let private personaBinding (role: Role) (tier: AgentTier) =
        PersonaCatalog.persona role tier
        |> fun value -> value.ToLowerInvariant(), ManagedAgent.make tier role

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

            bindings
            |> List.tryPick (fun (name, managed) -> if name = wanted then Some managed else None)

    let private hasCalling (request: Request) =
        not (String.IsNullOrWhiteSpace request.Calling)

    let private hasKeywords (request: Request) =
        not (String.IsNullOrWhiteSpace request.Keywords)

    let private warmStartAllowed role =
        RepositoryWarmStartPrompt.isDirectConsumer role

    let private prepareForkPrompt (scope: ToolRuntimeScope) (runtime: HostForkRuntime) (role: Role) (request: Request) =
        task {
            let! parentWorkRecord = runtime.ParentWorkRecordOf runtime.ParentId

            let basePrompt =
                ForkChildPayload.relay
                    (forkInstructions runtime.ParentId)
                    request.Charge
                    parentWorkRecord
                    []
                    None

            match!
                RepositoryWarmStart.appendToBase
                    runtime.ParentId
                    role
                    scope.WorkspaceDirectory
                    request.Keywords
                    basePrompt
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
            let language = lang context

            if String.IsNullOrWhiteSpace request.Name then
                return consequence (prose language Path.Fork.NameRequired)
            elif String.IsNullOrWhiteSpace request.Charge then
                return consequence (prose language Path.Fork.ChargeRequired)
            else
                match scope.RuntimeFor context with
                | Error _ -> return consequence (prose language Path.Fork.ChargeContextUnavailable)
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
                        | Some _ -> return consequence (prose language Path.Fork.NameAlreadyBelongs)
                        | None ->
                            match tryCalling managerCallingBindings request.Calling with
                            | None -> return consequence (prose language Path.Fork.UnknownCalling)
                            | Some managed ->
                                let role = AgentRoleIdentity.ofManaged managed.Role

                                if hasKeywords request && not (warmStartAllowed role) then
                                    return consequence (prose language Path.Fork.WarmStartUnavailable)
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
                                        return
                                            successInstruction (
                                                namedProse language Path.Fork.ChargeCarried (request.Name.Trim())
                                            )
                                    | Error _ -> return consequence (prose language Path.Fork.ChargeNotPlaced)
                    else
                        match existingByname with
                        | None -> return consequence (prose language Path.Fork.PersonUnknown)
                        | Some handle ->
                            match HandleId.tryAgent handle.Handle with
                            | None -> return consequence (prose language Path.Fork.PersonUnknown)
                            | Some handleId ->
                                let agentId = AgentHandleId.value handleId

                                match runtime.TryFindAgent agentId with
                                | None -> return consequence (prose language Path.Fork.PersonUnavailable)
                                | Some record when hasKeywords request && not (warmStartAllowed record.Role) ->
                                    return consequence (prose language Path.Fork.WarmStartUnavailable)
                                | Some record ->
                                    let activeRun =
                                        lock runtime.Gate (fun () -> runtime.PendingRuns.ContainsKey agentId)

                                    let! reuseResult =
                                        if hasKeywords request && not activeRun then
                                            task {
                                                let! rendered = prepareForkPrompt scope runtime record.Role request

                                                return!
                                                    runtime.Reuse(agentId, request.Charge, renderedPrompt = rendered)
                                            }
                                        else
                                            runtime.Reuse(agentId, request.Charge)

                                    match reuseResult with
                                    | Error _ -> return consequence (prose language Path.Fork.PersonCannotTakeCharge)
                                    | Ok _ ->
                                        return
                                            successInstruction (
                                                namedProse language Path.Fork.ChargeCarried (request.Name.Trim())
                                            )
        }

    let private executeOrchestrator (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            let language = lang context

            if String.IsNullOrWhiteSpace context.SessionId then
                return consequence (prose language Path.Commission.AuthorityRequired)
            elif String.IsNullOrWhiteSpace request.Name then
                return consequence (prose language Path.Commission.NameRequired)
            elif String.IsNullOrWhiteSpace request.Charge then
                return consequence (prose language Path.Commission.ChargeRequired)
            else
                let existingByname =
                    scope.Journal
                    |> Option.bind (fun journal ->
                        (AgentJournal.snapshot journal).AgentProjections.Orchestrator
                        |> OrchestratorProjection.tryFindByByname request.Name)

                if hasCalling request then
                    match existingByname with
                    | Some _ -> return consequence (prose language Path.Commission.NameAlreadyBelongs)
                    | None ->
                        match tryCalling orchestratorCallingBindings request.Calling with
                        | None -> return consequence (prose language Path.Commission.UnknownCalling)
                        | Some managed ->
                            let managerId = ManagerJobId.create (ToolHostCodec.newHandleId ())
                            let host = scope.OrchestratorHostFor context.SessionId

                            match!
                                host.ForkManagerJob(managerId, managed.Name, request.Charge, byname = request.Name)
                            with
                            | Ok _ ->
                                return
                                    successInstruction (
                                        namedProse language Path.Commission.ChargeTaken (request.Name.Trim())
                                    )
                            | Error _ -> return consequence (prose language Path.Commission.RoadNotOpened)
                else
                    match existingByname with
                    | None -> return consequence (prose language Path.Commission.RoadUnknown)
                    | Some job ->
                        let host = scope.OrchestratorHostFor context.SessionId

                        match! host.ContinueManagerJob(job.ManagerJobId, request.Charge) with
                        | Ok _ ->
                            return
                                successInstruction (
                                    namedProse language Path.Commission.ChargeTaken (request.Name.Trim())
                                )
                        | Error _ -> return consequence (prose language Path.Commission.RoadCannotTakeCharge)
        }

    let managerSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "fork"
          Description = prose language Path.Fork.Description
          Arguments =
            [ "calling",
              ToolHostCodec.optionalEnumSchemaDescribed
                  (callingNames managerCallingBindings)
                  (prose language Path.Fork.ArgCalling)
                  factory
              "name", ToolHostCodec.stringSchemaDescribed (prose language Path.Fork.ArgName) factory
              "charge", ToolHostCodec.stringSchemaDescribed (prose language Path.Fork.ArgCharge) factory
              "keywords", ToolHostCodec.optionalStringSchemaDescribed (prose language Path.Fork.ArgKeywords) factory ]
          Execute = fun args context -> executeManager scope (decode args) context }

    let orchestratorSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "commission"
          Description = prose language Path.Commission.Description
          Arguments =
            [ "calling",
              ToolHostCodec.optionalEnumSchemaDescribed
                  (callingNames orchestratorCallingBindings)
                  (prose language Path.Commission.ArgCalling)
                  factory
              "name", ToolHostCodec.stringSchemaDescribed (prose language Path.Commission.ArgName) factory
              "charge", ToolHostCodec.stringSchemaDescribed (prose language Path.Commission.ArgCharge) factory ]
          Execute = fun args context -> executeOrchestrator scope (decode args) context }
