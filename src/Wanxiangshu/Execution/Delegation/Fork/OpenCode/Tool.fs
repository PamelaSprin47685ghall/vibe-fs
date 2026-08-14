namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open System
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Change
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

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
            let ArgAttach = "delegation/fork-attach-argument"

            [<Literal>]
            let AttachUnknown = "delegation/fork-attach-unknown"

            [<Literal>]
            let AttachSelf = "delegation/fork-attach-self"

            [<Literal>]
            let AttachBusy = "delegation/fork-attach-busy"

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
          Attachment = ProviderProse.render lang ForkChildPayload.AttachmentPath Map.empty
          Requirements = ProviderProse.render lang ForkChildPayload.RequirementsPath Map.empty }

    type Request =
        { Calling: string
          Name: string
          Charge: string
          Keywords: string
          Attach: string option
          ExpectedToolCalls: int option }

    let private decode language (args: HostToolArguments) =
        match DelegatedToolEstimate.decode args with
        | Error _ -> Error(DelegatedToolEstimate.invalid language)
        | Ok expectedToolCalls ->
            Ok
                { Calling = args.Text "calling"
                  Name = args.Text "name"
                  Charge = args.Text "charge"
                  Keywords = args.Text "keywords"
                  Attach = args.OptionalText "attach" |> Option.map (fun value -> value.Trim())
                  ExpectedToolCalls = expectedToolCalls }

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

    let private prepareForkPrompt
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (role: Role)
        (request: Request)
        (attachment: string option)
        =
        task {
            let! parentWorkRecord = runtime.ParentWorkRecordOf runtime.ParentId

            let basePrompt =
                ForkChildPayload.relay
                    (forkInstructions runtime.ParentId)
                    request.Charge
                    parentWorkRecord
                    attachment
                    []
                    None

            if hasKeywords request then
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
            else
                return basePrompt
        }

    let private isSelfAttachment (request: Request) =
        request.Attach
        |> Option.exists (fun attach ->
            System.String.Equals(attach, request.Name.Trim(), System.StringComparison.OrdinalIgnoreCase))

    let private resolveAttachment
        (scope: ToolRuntimeScope)
        (handles: AgentLinkageProjection option)
        (request: Request)
        =
        task {
            match request.Attach with
            | None -> return Ok None
            | Some attach ->
                match handles |> Option.bind (HandleProjection.tryFindByByname attach) with
                | None -> return Error Path.Fork.AttachUnknown
                | Some record ->
                    let! workRecord = scope.ParentWorkRecordFor(SessionId.value record.ChildSessionId)
                    return Ok workRecord
        }

    let private bynameOf (request: Request) (fallback: string) =
        if String.IsNullOrWhiteSpace request.Name then
            fallback
        else
            request.Name.Trim()

    let private recordFissionAffinity (scope: ToolRuntimeScope) (context: HostToolContext) handleId =
        task {
            if String.IsNullOrWhiteSpace context.SessionId then
                return Ok()
            else
                match FissionRuntime.tryLane (SessionId.create context.SessionId), scope.Journal with
                | Some lane, Some durable ->
                    match!
                        AgentJournal.appendAgent
                            (StreamId.Session lane.OwnerSessionId)
                            context.ProviderRunId
                            (FissionFact.FissionExternalAffinityBound
                                {| GroupId = lane.GroupId
                                   OwnerSessionId = lane.OwnerSessionId
                                   ExternalId = FissionExternalId.agent handleId
                                   LaneIndex = lane.LaneIndex |})
                            durable
                    with
                    | Ok _ -> return Ok()
                    | Error failure -> return Error(JournalAppendFailure.describe failure)
                | _ -> return Ok()
        }

    let private executeManager (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            let language = lang context

            if String.IsNullOrWhiteSpace request.Name then
                return consequence (prose language Path.Fork.NameRequired)
            elif String.IsNullOrWhiteSpace request.Charge then
                return consequence (prose language Path.Fork.ChargeRequired)
            elif isSelfAttachment request then
                return consequence (prose language Path.Fork.AttachSelf)
            else
                match scope.RuntimeFor context with
                | Error _ -> return consequence (prose language Path.Fork.ChargeContextUnavailable)
                | Ok runtime ->
                    let handles =
                        match scope.Journal with
                        | Some journal when not (String.IsNullOrWhiteSpace context.SessionId) ->
                            let owner = scope.LogicalOwnerFor(SessionId.create context.SessionId)
                            Some(AgentJournal.handleProjection journal owner)
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
                                    match! resolveAttachment scope handles request with
                                    | Error path -> return consequence (prose language path)
                                    | Ok attachment ->
                                        let handleId = ToolHostCodec.newHandleId ()

                                        let! forkResult =
                                            if hasKeywords request || request.Attach.IsSome then
                                                task {
                                                    let! rendered =
                                                        prepareForkPrompt scope runtime role request attachment

                                                    return!
                                                        runtime.Fork(
                                                            handleId,
                                                            role,
                                                            managed.Name,
                                                            request.Charge,
                                                            None,
                                                            renderedPrompt = rendered,
                                                            byname = request.Name,
                                                            ?expectedToolCalls = request.ExpectedToolCalls
                                                        )
                                                }
                                            else
                                                runtime.Fork(
                                                    handleId,
                                                    role,
                                                    managed.Name,
                                                    request.Charge,
                                                    None,
                                                    byname = request.Name,
                                                    ?expectedToolCalls = request.ExpectedToolCalls
                                                )

                                        match forkResult with
                                        | Ok _ ->
                                            match! recordFissionAffinity scope context handleId with
                                            | Error _ ->
                                                return consequence (prose language Path.Fork.ChargeNotPlaced)
                                            | Ok() ->
                                                runtime.TryFindAgent handleId
                                                |> Option.bind (fun created -> created.ChildSessionId)
                                                |> Option.iter (fun childId ->
                                                    FissionRuntime.notifyChildCreated
                                                        (SessionId.create context.SessionId)
                                                        handleId
                                                        childId)

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

                                    if activeRun then
                                        match!
                                            runtime.Reuse(
                                                agentId,
                                                request.Charge,
                                                ?expectedToolCalls = request.ExpectedToolCalls
                                            )
                                        with
                                        | Error _ ->
                                            return consequence (prose language Path.Fork.PersonCannotTakeCharge)
                                        | Ok _ when request.Attach.IsSome ->
                                            return
                                                successInstruction (
                                                    namedProse language Path.Fork.AttachBusy (request.Name.Trim())
                                                )
                                        | Ok _ ->
                                            return
                                                successInstruction (
                                                    namedProse language Path.Fork.ChargeCarried (request.Name.Trim())
                                                )
                                    else
                                        match! resolveAttachment scope handles request with
                                        | Error path -> return consequence (prose language path)
                                        | Ok attachment ->
                                            let! reuseResult =
                                                if hasKeywords request || request.Attach.IsSome then
                                                    task {
                                                        let! rendered =
                                                            prepareForkPrompt
                                                                scope
                                                                runtime
                                                                record.Role
                                                                request
                                                                attachment

                                                        return!
                                                            runtime.Reuse(
                                                                agentId,
                                                                request.Charge,
                                                                renderedPrompt = rendered,
                                                                ?expectedToolCalls = request.ExpectedToolCalls
                                                            )
                                                    }
                                                else
                                                    runtime.Reuse(
                                                        agentId,
                                                        request.Charge,
                                                        ?expectedToolCalls = request.ExpectedToolCalls
                                                    )

                                            match reuseResult with
                                            | Error _ ->
                                                return consequence (prose language Path.Fork.PersonCannotTakeCharge)
                                            | Ok _ ->
                                                match! recordFissionAffinity scope context agentId with
                                                | Error _ ->
                                                    return consequence (prose language Path.Fork.PersonCannotTakeCharge)
                                                | Ok() ->
                                                    runtime.TryFindAgent agentId
                                                    |> Option.bind (fun updated -> updated.ChildSessionId)
                                                    |> Option.iter (fun childId ->
                                                        FissionRuntime.notifyChildCreated
                                                            (SessionId.create context.SessionId)
                                                            agentId
                                                            childId)

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
                                host.ForkManagerJob(
                                    managerId,
                                    managed.Name,
                                    request.Charge,
                                    byname = request.Name,
                                    ?expectedToolCalls = request.ExpectedToolCalls
                                )
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

                        match!
                            host.ContinueManagerJob(
                                job.ManagerJobId,
                                request.Charge,
                                ?expectedToolCalls = request.ExpectedToolCalls
                            )
                        with
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
              "keywords", ToolHostCodec.optionalStringSchemaDescribed (prose language Path.Fork.ArgKeywords) factory
              "attach", ToolHostCodec.optionalStringSchemaDescribed (prose language Path.Fork.ArgAttach) factory
              "expected_tool_calls", DelegatedToolEstimate.schema language factory ]
          Execute =
            fun args context ->
                task {
                    let language = lang context

                    match decode language args with
                    | Error message -> return consequence message
                    | Ok request -> return! executeManager scope request context
                } }

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
              "charge", ToolHostCodec.stringSchemaDescribed (prose language Path.Commission.ArgCharge) factory
              "expected_tool_calls", DelegatedToolEstimate.schema language factory ]
          Execute =
            fun args context ->
                task {
                    let language = lang context

                    match decode language args with
                    | Error message -> return consequence message
                    | Ok request -> return! executeOrchestrator scope request context
                } }
