namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Composition.Durable

open System
open System.Threading.Tasks
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
open FsToolkit.ErrorHandling
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

            [<Literal>]
            let HandoffJournalRequired = "tool/fork/handoff-journal-required"

            [<Literal>]
            let PersonSessionUnknown = "tool/fork/person-session-unknown"

            [<Literal>]
            let HandoffAppendFailed = "tool/fork/handoff-append-failed"

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

    let private reasonProse language path reason =
        ProviderProse.render language path (Map [ "reason", reason ])

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

    let private prepareForkPromptWithRecord
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (role: Role)
        (request: Request)
        (commissionerRecord: string option)
        (attachment: string option)
        =
        task {
            let basePrompt =
                ForkChildPayload.relay
                    (forkInstructions runtime.ParentId)
                    request.Charge
                    commissionerRecord
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
        : Task<Result<string option, string>> =
        match request.Attach with
        | None -> Task.FromResult(Ok None)
        | Some attach ->
            taskResult {
                let! record =
                    handles
                    |> Option.bind (HandleProjection.tryFindByByname attach)
                    |> Result.requireSome Path.Fork.AttachUnknown

                let! workRecord =
                    scope.ParentWorkRecordFor(SessionId.value record.ChildSessionId)
                    |> TaskResultCE.ofTask

                return workRecord
            }

    let private appendFissionAffinity durable (context: HostToolContext) (lane: FissionLaneBinding) handleId =
        taskResult {
            let! _ =
                task {
                    let! result =
                        AgentJournal.appendAgent
                            (StreamId.Session lane.OwnerSessionId)
                            context.ProviderRunId
                            (FissionFact.FissionExternalAffinityBound
                                {| GroupId = lane.GroupId
                                   OwnerSessionId = lane.OwnerSessionId
                                   ExternalId = FissionExternalId.agent handleId
                                   LaneIndex = lane.LaneIndex |})
                            durable

                    return Result.mapError JournalAppendFailure.describe result
                }

            return ()
        }

    let private bindFissionAffinity (scope: ToolRuntimeScope) (context: HostToolContext) handleId =
        match FissionRuntime.tryLane (SessionId.create context.SessionId), scope.Journal with
        | Some lane, Some durable -> appendFissionAffinity durable context lane handleId
        | _ -> Task.FromResult(Ok())

    let private recordFissionAffinity (scope: ToolRuntimeScope) (context: HostToolContext) handleId =
        if String.IsNullOrWhiteSpace context.SessionId then
            Task.FromResult(Ok())
        else
            bindFissionAffinity scope context handleId

    let private agentHandles (scope: ToolRuntimeScope) (context: HostToolContext) =
        match scope.Journal with
        | Some journal when not (String.IsNullOrWhiteSpace context.SessionId) ->
            let owner = scope.LogicalOwnerFor(SessionId.create context.SessionId)
            Some(AgentJournal.handleProjection journal owner)
        | _ -> None

    let private announceChild (runtime: HostForkRuntime) (context: HostToolContext) agentKey =
        runtime.TryFindAgent agentKey
        |> Option.bind (fun created -> created.ChildSessionId)
        |> Option.iter (fun childId ->
            FissionRuntime.notifyChildCreated (SessionId.create context.SessionId) agentKey childId)

    let private runManagerFork
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        role
        (request: Request)
        language
        attachment
        handleId
        (managed: ManagedAgent)
        =
        taskResult {
            let! durable =
                scope.Journal
                |> Result.requireSome (prose language Path.Fork.HandoffJournalRequired)

            let! handoff =
                DelegationHandoffLedger.prepareInitial durable runtime.ParentId
                |> TaskResultCE.ofTask

            let! rendered =
                prepareForkPromptWithRecord scope runtime role request handoff.ParentRecord attachment
                |> TaskResultCE.ofTask

            let! result =
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

            let! childId =
                runtime.TryFindAgent handleId
                |> Option.bind (fun record -> record.ChildSessionId)
                |> Result.requireSome (namedProse language Path.Fork.PersonSessionUnknown handleId)

            do!
                runtime.AdvanceHandoff(childId, handoff.ParentEndExclusive)
                |> TaskResult.mapError (reasonProse language Path.Fork.HandoffAppendFailed)

            return result
        }

    let private runManagerReuse
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        role
        (request: Request)
        language
        attachment
        agentId
        =
        taskResult {
            let! childId =
                runtime.TryFindAgent agentId
                |> Option.bind (fun record -> record.ChildSessionId)
                |> Result.requireSome (namedProse language Path.Fork.PersonSessionUnknown agentId)

            let! handoff = runtime.PrepareHandoff childId |> TaskResultCE.ofTask

            let! rendered =
                prepareForkPromptWithRecord scope runtime role request handoff.ParentRecord attachment
                |> TaskResultCE.ofTask

            let! _ =
                runtime.Reuse(
                    agentId,
                    request.Charge,
                    renderedPrompt = rendered,
                    ?expectedToolCalls = request.ExpectedToolCalls
                )

            do!
                runtime.AdvanceHandoff(childId, handoff.ParentEndExclusive)
                |> TaskResult.mapError (reasonProse language Path.Fork.HandoffAppendFailed)

            return! runtime.AwaitCurrentWorkRecord agentId
        }

    let private sealManagerAffinity
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        agentKey
        language
        (request: Request)
        denyPath
        =
        task {
            match! recordFissionAffinity scope context agentKey with
            | Error _ -> return consequence (prose language denyPath)
            | Ok() ->
                announceChild runtime context agentKey

                return successInstruction (namedProse language Path.Fork.ChargeCarried (request.Name.Trim()))
        }

    let private commitNewManagerFork
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        (request: Request)
        language
        (managed: ManagedAgent)
        role
        attachment
        =
        task {
            let handleId = ToolHostCodec.newHandleId ()
            let! forkResult = runManagerFork scope runtime role request language attachment handleId managed

            match forkResult with
            | Error _ -> return consequence (prose language Path.Fork.ChargeNotPlaced)
            | Ok _ ->
                return! sealManagerAffinity scope runtime context handleId language request Path.Fork.ChargeNotPlaced
        }

    let private sealIdleReuse
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        language
        agentId
        workRecord
        =
        task {
            match! recordFissionAffinity scope context agentId with
            | Error _ -> return consequence (prose language Path.Fork.PersonCannotTakeCharge)
            | Ok() ->
                announceChild runtime context agentId
                return ToolHostCodec.tomlObjectWithInstructions [ workRecord ] []
        }

    let private finishNewManagerFork
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        (request: Request)
        language
        handles
        (managed: ManagedAgent)
        role
        =
        task {
            match! resolveAttachment scope handles request with
            | Error path -> return consequence (prose language path)
            | Ok attachment ->
                return! commitNewManagerFork scope runtime context request language managed role attachment
        }

    let private placeNewManagerFork
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        (request: Request)
        language
        handles
        (managed: ManagedAgent)
        =
        let role = AgentRoleIdentity.ofManaged managed.Role

        if hasKeywords request && not (warmStartAllowed role) then
            Task.FromResult(consequence (prose language Path.Fork.WarmStartUnavailable))
        else
            finishNewManagerFork scope runtime context request language handles managed role

    let private reuseWhileActive (runtime: HostForkRuntime) agentId (request: Request) language =
        task {
            match! runtime.Reuse(agentId, request.Charge, ?expectedToolCalls = request.ExpectedToolCalls) with
            | Error _ -> return consequence (prose language Path.Fork.PersonCannotTakeCharge)
            | Ok _ when request.Attach.IsSome ->
                return successInstruction (namedProse language Path.Fork.AttachBusy (request.Name.Trim()))
            | Ok _ -> return successInstruction (namedProse language Path.Fork.ChargeCarried (request.Name.Trim()))
        }

    let private commitIdleReuse
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        (request: Request)
        language
        (record: AgentRecord)
        agentId
        attachment
        =
        task {
            let! reuseResult = runManagerReuse scope runtime record.Role request language attachment agentId

            match reuseResult with
            | Error _ -> return consequence (prose language Path.Fork.PersonCannotTakeCharge)
            | Ok workRecord -> return! sealIdleReuse scope runtime context language agentId workRecord
        }

    let private reuseWhileIdle
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        (request: Request)
        language
        handles
        (record: AgentRecord)
        agentId
        =
        task {
            match! resolveAttachment scope handles request with
            | Error path -> return consequence (prose language path)
            | Ok attachment -> return! commitIdleReuse scope runtime context request language record agentId attachment
        }

    let private reuseFoundAgent
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        (request: Request)
        language
        handles
        (record: AgentRecord)
        agentId
        =
        let activeRun =
            lock runtime.Gate (fun () -> runtime.PendingRuns.ContainsKey agentId)

        if activeRun then
            reuseWhileActive runtime agentId request language
        else
            reuseWhileIdle scope runtime context request language handles record agentId

    let private reuseResolvedAgent
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        (request: Request)
        language
        handles
        agentId
        =
        match runtime.TryFindAgent agentId with
        | None -> Task.FromResult(consequence (prose language Path.Fork.PersonUnavailable))
        | Some record when hasKeywords request && not (warmStartAllowed record.Role) ->
            Task.FromResult(consequence (prose language Path.Fork.WarmStartUnavailable))
        | Some record -> reuseFoundAgent scope runtime context request language handles record agentId

    let private executeManagerReusePerson
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        (request: Request)
        language
        handles
        handle
        =
        match HandleId.tryAgent handle.Handle with
        | None -> Task.FromResult(consequence (prose language Path.Fork.PersonUnknown))
        | Some handleId ->
            reuseResolvedAgent scope runtime context request language handles (AgentHandleId.value handleId)

    let private executeManagerNewCalling
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        (request: Request)
        language
        handles
        existingByname
        =
        match existingByname, tryCalling managerCallingBindings request.Calling with
        | Some _, _ -> Task.FromResult(consequence (prose language Path.Fork.NameAlreadyBelongs))
        | None, None -> Task.FromResult(consequence (prose language Path.Fork.UnknownCalling))
        | None, Some managed -> placeNewManagerFork scope runtime context request language handles managed

    let private executeManagerExistingPerson
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (context: HostToolContext)
        (request: Request)
        language
        handles
        existingByname
        =
        match existingByname with
        | None -> Task.FromResult(consequence (prose language Path.Fork.PersonUnknown))
        | Some handle -> executeManagerReusePerson scope runtime context request language handles handle

    let private executeManagerWithRuntime
        (scope: ToolRuntimeScope)
        (runtime: HostForkRuntime)
        (request: Request)
        (context: HostToolContext)
        language
        =
        let handles = agentHandles scope context

        let existingByname =
            handles |> Option.bind (HandleProjection.tryFindByByname request.Name)

        if hasCalling request then
            executeManagerNewCalling scope runtime context request language handles existingByname
        else
            executeManagerExistingPerson scope runtime context request language handles existingByname

    let private executeManagerAfterGuards
        (scope: ToolRuntimeScope)
        (request: Request)
        (context: HostToolContext)
        language
        =
        match scope.RuntimeFor context with
        | Error _ -> Task.FromResult(consequence (prose language Path.Fork.ChargeContextUnavailable))
        | Ok runtime -> executeManagerWithRuntime scope runtime request context language

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
                return! executeManagerAfterGuards scope request context language
        }

    let private orchestratorExistingByname (scope: ToolRuntimeScope) (request: Request) =
        scope.Journal
        |> Option.bind (fun journal ->
            (AgentJournal.snapshot journal).AgentProjections.Orchestrator
            |> OrchestratorProjection.tryFindByByname request.Name)

    let private finishCommissionNew
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (request: Request)
        language
        (managed: ManagedAgent)
        =
        task {
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
            | Ok _ -> return successInstruction (namedProse language Path.Commission.ChargeTaken (request.Name.Trim()))
            | Error _ -> return consequence (prose language Path.Commission.RoadNotOpened)
        }

    let private commissionNewCalling
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (request: Request)
        language
        existingByname
        =
        match existingByname, tryCalling orchestratorCallingBindings request.Calling with
        | Some _, _ -> Task.FromResult(consequence (prose language Path.Commission.NameAlreadyBelongs))
        | None, None -> Task.FromResult(consequence (prose language Path.Commission.UnknownCalling))
        | None, Some managed -> finishCommissionNew scope context request language managed

    let private continueExistingCommission
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (request: Request)
        language
        job
        =
        task {
            let host = scope.OrchestratorHostFor context.SessionId

            match!
                host.ContinueManagerJob(
                    job.ManagerJobId,
                    request.Charge,
                    ?expectedToolCalls = request.ExpectedToolCalls
                )
            with
            | Ok _ -> return successInstruction (namedProse language Path.Commission.ChargeTaken (request.Name.Trim()))
            | Error _ -> return consequence (prose language Path.Commission.RoadCannotTakeCharge)
        }

    let private commissionExistingByname
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (request: Request)
        language
        existingByname
        =
        match existingByname with
        | None -> Task.FromResult(consequence (prose language Path.Commission.RoadUnknown))
        | Some job -> continueExistingCommission scope context request language job

    let private executeOrchestratorAfterGuards
        (scope: ToolRuntimeScope)
        (request: Request)
        (context: HostToolContext)
        language
        =
        let existingByname = orchestratorExistingByname scope request

        if hasCalling request then
            commissionNewCalling scope context request language existingByname
        else
            commissionExistingByname scope context request language existingByname

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
                return! executeOrchestratorAfterGuards scope request context language
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
