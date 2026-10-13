namespace Wanxiangshu.Mission.Relay.OpenCode

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Git
open Wanxiangshu.Host
open Wanxiangshu.Mission.Relay
open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

module SuicideTool =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/deliver/description"

        [<Literal>]
        let Blocked = "tool/deliver/blocked"

        [<Literal>]
        let AssessmentRequired = "tool/deliver/assessment-required"

        [<Literal>]
        let Retired = "tool/deliver/finished"

        [<Literal>]
        let Confirmation = "tool/deliver/confirmation"

        [<Literal>]
        let NonManagerRole = "tool/deliver/non-manager-role"

        [<Literal>]
        let NoAuthority = "tool/deliver/no-authority"

        [<Literal>]
        let NoRetirementProjection = "tool/deliver/finish-not-recorded"

        [<Literal>]
        let BindingUnavailable = "tool/deliver/binding-unavailable"

        [<Literal>]
        let FinishFailed = "tool/deliver/finish-failed"

    let private text (path: string) =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path Map.empty

    let private pendingDeferred (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.pendingAttentionWorkPairs sessionId (AgentJournal.snapshot journal).AgentProjections

    let private currentState (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Relay)

    let private currentView state roadId =
        state |> Option.bind (fun relay -> Fold.view relay roadId)

    let private deterministicId prefix payload create =
        HostDigest.sha256Hex (prefix + "\n" + payload)
        |> fun digest -> create (prefix + ":" + digest)

    let private blockedResult blockers =
        ToolHostCodec.tomlObjectWithInstructions
            [ text Path.Blocked ]
            [ "finished", ToolHostCodec.TBool false
              "blocker_count", ToolHostCodec.TInt(List.length blockers)
              "blockers",
              ToolHostCodec.TTable(
                  blockers
                  |> List.mapi (fun index blocker -> string index, ToolHostCodec.TString blocker)
              ) ]

    let private assessmentRequiredResult () =
        ToolHostCodec.tomlObjectWithInstructions
            [ text Path.AssessmentRequired ]
            [ "finished", ToolHostCodec.TBool false
              "assessment_required", ToolHostCodec.TBool true ]

    let private retiredResult () =
        ToolHostCodec.tomlObjectWithInstructions [ text Path.Retired ] [ "finished", ToolHostCodec.TBool true ]

    let private requireSome error =
        function
        | Some value -> Ok value
        | None -> Error error

    type private BoundRetirement =
        { ToolCallId: ToolCallId
          ProviderRun: ProviderRunIdentity
          Directory: string
          Journal: AgentJournal }

    type private PreparedRetirement =
        { Bound: BoundRetirement
          SessionId: SessionId
          RoadId: RoadId
          View: RoadView
          Authority: AuthorityRevision
          Incumbent: IncumbencyId }

    type private FreezeTarget =
        { Bound: BoundRetirement
          SessionId: SessionId
          RoadId: RoadId
          Incumbent: IncumbencyId }

    let private requireView roadId state =
        currentView state roadId |> requireSome (text Path.BindingUnavailable)

    let private requireActiveIncumbency (road: RoadView) =
        road.ActiveIncumbency |> requireSome (text Path.BindingUnavailable)

    let private requireSameIncumbency (target: FreezeTarget) (road: RoadView) =
        road.ActiveIncumbency
        |> Option.filter ((=) target.Incumbent)
        |> requireSome (text Path.BindingUnavailable)

    let private bindInvocation (scope: ToolRuntimeScope) (context: HostToolContext) =
        match
            scope.RoleFor context,
            context.ToolCallId,
            context.ProviderRunId,
            scope.CurrentPhysicalUserMessage context.SessionId,
            scope.WorkspaceDirectory,
            scope.Journal
        with
        | Some Role.Manager, Some toolCallId, Some providerRun, Some _, Some directory, Some journal when
            not (String.IsNullOrWhiteSpace context.SessionId)
            ->
            Ok
                { ToolCallId = toolCallId
                  ProviderRun = providerRun
                  Directory = directory
                  Journal = journal }
        | Some Role.Manager, _, _, _, _, _ -> Error(text Path.BindingUnavailable)
        | Some _, _, _, _, _, _ -> Error(text Path.NonManagerRole)
        | None, _, _, _, _, _ -> Error(text Path.NoAuthority)

    let private prepareTarget (context: HostToolContext) (bound: BoundRetirement) =
        let sessionId = SessionId.create context.SessionId
        let roadId = RoadId.create context.SessionId
        let state = currentState bound.Journal sessionId

        result {
            let! road = requireView roadId state
            let! incumbent = requireActiveIncumbency road

            return
                { Bound = bound
                  SessionId = sessionId
                  RoadId = roadId
                  Incumbent = incumbent }
        }

    let private retirementFromProjection (prepared: PreparedRetirement) projection =
        AgentProjection.tryFind prepared.SessionId projection.AgentProjections
        |> Option.bind (fun session -> session.Relay)
        |> Option.bind (fun relay -> Fold.view relay prepared.RoadId)
        |> Option.bind (fun road -> road.LatestRetirement)
        |> requireSome (text Path.NoRetirementProjection)

    let private confirmationResult (prepared: PreparedRetirement) pending =
        let commitments =
            prepared.View.AcceptedAssessmentFindings
            |> Option.map AssessmentFindings.values
            |> Option.defaultValue []
            |> List.mapi (fun index finding ->
                string index, ToolHostCodec.TString(finding.AcceptanceCriteria + " => " + finding.WorkPlan))

        let deferred =
            pending
            |> List.mapi (fun index (_, itemText) -> string index, ToolHostCodec.TString itemText)

        let optionalFields =
            [ if not (List.isEmpty commitments) then
                  yield "commitments", ToolHostCodec.TTable commitments

              if not (List.isEmpty deferred) then
                  yield "deferred", ToolHostCodec.TTable deferred ]

        ToolHostCodec.tomlObjectWithInstructions
            [ text Path.Confirmation ]
            ([ "finished", ToolHostCodec.TBool false
               "confirmation_required", ToolHostCodec.TBool true ]
             @ optionalFields)

    let private runBlocked (prepared: PreparedRetirement) blockers =
        ignore prepared
        ignore blockers
        Task.FromResult(Error "blocked retirement is written only by the lifecycle append")

    /// ATTENTION-005: a completed retirement consumes this life's remaining
    /// deferred work and leaves a durable consumption receipt, so a replayed
    /// `DeferredWorkRecorded` cannot resurrect it after restart.
    let private consumePendingDeferred (prepared: PreparedRetirement) =
        task {
            let port = AttentionConcernJournalAdapter.forAttention prepared.Bound.Journal
            let pending = AttentionProjection.pending prepared.SessionId (port.Read())

            match pending with
            | [] -> return ()
            | items ->
                let fact =
                    AttentionFactCases.DeferredWorkConsumed
                        {| SessionId = prepared.SessionId
                           OccurrenceIds = (items |> List.map (fun item -> item.OccurrenceId)) |}

                let! _ = port.Append prepared.SessionId (Some prepared.Bound.ProviderRun) fact
                return ()
        }

    let private runRetirement (prepared: PreparedRetirement) =
        Task.FromResult(Error "retirement is written only by the lifecycle append")

    // ATTENTION-005: the completed retirement consumes this life's
    // remaining deferred work. An append failure leaves the outcome
    // unchanged; `RetirementCommitted` stays durable either way.
    let private completeRetirement (prepared: PreparedRetirement) =
        task {
            let! outcome = runRetirement prepared

            match outcome with
            | Ok value ->
                do! consumePendingDeferred prepared
                return Ok value
            | Error error -> return Error error
        }

    let private runRetirementOrBlocked
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (prepared: PreparedRetirement)
        =
        let blockers = scope.RetirementBlockersFor context.SessionId

        if List.isEmpty blockers then
            completeRetirement prepared
        else
            runBlocked prepared blockers

    let private runConfirmed
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (prepared: PreparedRetirement)
        pending
        toolCallId
        providerRunId
        confirmFirst
        =
        let confirmed =
            scope.WorkspaceDirectory
            |> Option.map RuntimePath.gitCommonDir
            |> Option.bind (fun dir ->
                if String.IsNullOrWhiteSpace dir then
                    None
                else
                    WorkspaceEventStore.tryCurrent dir)
            |> Option.bind (fun held -> held.TryCurrent "Lifecycle")
            |> Option.bind (fun current ->
                let works = unbox<Map<string, Wanxiangshu.Mission.Lifecycle.LifecycleWork>> current
                Map.tryFind context.SessionId works)
            |> Option.bind (fun work -> work.Active)
            |> Option.bind (fun active -> active.Confirmed)

        match confirmed with
        | None -> confirmFirst ()
        | Some(confirmedRun, confirmedCall) when confirmedCall = toolCallId && confirmedRun = providerRunId ->
            scope.UnfreezeRetirement context.SessionId
            Task.FromResult(Ok(confirmationResult prepared pending))
        | Some(_, confirmedCall) when confirmedCall = toolCallId ->
            Task.FromResult(Error "RetirementConfirmationReplayConflict")
        | Some _ ->
            task {
                let directory = scope.WorkspaceDirectory
                let commonDir = directory |> Option.map RuntimePath.gitCommonDir
                let store =
                    match commonDir with
                    | None -> None
                    | Some dir when String.IsNullOrWhiteSpace dir -> None
                    | Some dir ->
                        match WorkspaceEventStore.tryCurrent dir with
                        | Some held -> Some held
                        | None -> Some(WorkspaceEventStore.acquire dir)

                match store with
                | None -> return Error "lifecycle store is unavailable"
                | Some held ->
                    match held.TryCurrent "Lifecycle" with
                    | None -> return Error "lifecycle work is missing"
                    | Some current ->
                        let works = unbox<Map<string, Wanxiangshu.Mission.Lifecycle.LifecycleWork>> current
                        match Map.tryFind context.SessionId works with
                        | None -> return Error "lifecycle work is missing"
                        | Some work ->
                            let fence =
                                { Wanxiangshu.Mission.Lifecycle.HostFence.ProviderRunId = Some providerRunId
                                  Wanxiangshu.Mission.Lifecycle.HostFence.ToolCallId = Some toolCallId }

                            let blockerDigest =
                                let blockers = scope.RetirementBlockersFor context.SessionId
                                if List.isEmpty blockers then None else Some(String.concat "\n" blockers)

                            match
                                Wanxiangshu.Mission.Lifecycle.LifecycleKernel.deliverExecution
                                    HostDigest.sha256Hex
                                    work
                                    fence
                                    None
                                    blockerDigest
                            with
                            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Refuse reason -> return Error reason
                            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Done _ ->
                                scope.UnfreezeRetirement context.SessionId
                                return Ok(confirmationResult prepared pending)
                            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Write(events, _) ->
                                match! Wanxiangshu.Mission.Lifecycle.LifecycleKernel.commit held context.SessionId events with
                                | Ok(Wanxiangshu.Mission.Lifecycle.CommitOutcome.AppendDurable) ->
                                    scope.UnfreezeRetirement context.SessionId
                                    return Ok(text Path.Retired)
                                | Ok(Wanxiangshu.Mission.Lifecycle.CommitOutcome.AppendNotAttempted) ->
                                    return Error "retirement was not committed"
                                | Ok(Wanxiangshu.Mission.Lifecycle.CommitOutcome.CommitUnknown) ->
                                    return Error "retirement commit is unknown"
                                | Error reason -> return Error reason
            }

    let private runFrozen (scope: ToolRuntimeScope) (context: HostToolContext) (prepared: PreparedRetirement) =
        let hasAssessment = prepared.View.AcceptedAssessmentTransport |> Option.isSome
        let toolCallId = ToolCallId.value prepared.Bound.ToolCallId
        let providerRunId = ProviderRunIdentity.value prepared.Bound.ProviderRun

        let pending = pendingDeferred prepared.Bound.Journal prepared.SessionId

        let confirmFirst () =
            task {
                let directory = scope.WorkspaceDirectory
                let commonDir = directory |> Option.map RuntimePath.gitCommonDir
                let store =
                    match commonDir with
                    | None -> None
                    | Some dir when String.IsNullOrWhiteSpace dir -> None
                    | Some dir ->
                        match WorkspaceEventStore.tryCurrent dir with
                        | Some held -> Some held
                        | None -> Some(WorkspaceEventStore.acquire dir)

                match store with
                | None -> return Error "lifecycle store is unavailable"
                | Some held ->
                    match held.TryCurrent "Lifecycle" with
                    | None -> return Error "lifecycle work is missing"
                    | Some current ->
                        let works = unbox<Map<string, Wanxiangshu.Mission.Lifecycle.LifecycleWork>> current
                        match Map.tryFind context.SessionId works with
                        | None -> return Error "lifecycle work is missing"
                        | Some work ->
                            let fence =
                                { Wanxiangshu.Mission.Lifecycle.HostFence.ProviderRunId = Some providerRunId
                                  Wanxiangshu.Mission.Lifecycle.HostFence.ToolCallId = Some toolCallId }

                            let blockerDigest =
                                let blockers = scope.RetirementBlockersFor context.SessionId
                                if List.isEmpty blockers then None else Some(String.concat "\n" blockers)

                            match
                                Wanxiangshu.Mission.Lifecycle.LifecycleKernel.deliverExecution
                                    HostDigest.sha256Hex
                                    work
                                    fence
                                    None
                                    blockerDigest
                            with
                            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Refuse reason -> return Error reason
                            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Done _ ->
                                scope.UnfreezeRetirement context.SessionId
                                return Ok(confirmationResult prepared pending)
                            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Write(events, label) ->
                                match! Wanxiangshu.Mission.Lifecycle.LifecycleKernel.commit held context.SessionId events with
                                | Ok(Wanxiangshu.Mission.Lifecycle.CommitOutcome.AppendDurable) ->
                                    scope.UnfreezeRetirement context.SessionId
                                    if label = "confirmed" then
                                        return Ok(confirmationResult prepared pending)
                                    else
                                        return Ok(text Path.Retired)
                                | Ok(Wanxiangshu.Mission.Lifecycle.CommitOutcome.AppendNotAttempted) ->
                                    return Error "retirement was not committed"
                                | Ok(Wanxiangshu.Mission.Lifecycle.CommitOutcome.CommitUnknown) ->
                                    return Error "retirement commit is unknown"
                                | Error reason -> return Error reason
            }

        if not hasAssessment then
            scope.UnfreezeRetirement context.SessionId
            Task.FromResult(Ok(assessmentRequiredResult ()))
        else
            runConfirmed scope context prepared pending toolCallId providerRunId confirmFirst

    let private unfreezeUnlessRetired (scope: ToolRuntimeScope) (context: HostToolContext) =
        let facts = scope.ManagerCapabilityFactsFor context.SessionId

        if facts.HasActiveIncumbency then
            scope.UnfreezeRetirement context.SessionId

    let private finishOutcome (scope: ToolRuntimeScope) (context: HostToolContext) outcome =
        match outcome with
        | Ok value -> value
        | Error error ->
            scope.UnfreezeRetirement context.SessionId
            raise (InvalidOperationException error)

    // No session-scoped physical cut here. AbortSession names a session, not the
    // retired run; the Host applies it after an unobservable delay, so on a reused
    // session it lands inside the next loop iteration and kills it mid-tool. Retired
    // output is contained by the durable cut (ProjectionCut.ProviderRunId) plus Retired
    // tool denial; the retired run's further provider requests are refused in
    // the transform hook by exact manager-loop gate identity, so the run ends there.
    let private refreshAfterFreeze (context: HostToolContext) (target: FreezeTarget) =
        try
            result {
                let state = currentState target.Bound.Journal target.SessionId
                let! road = requireView target.RoadId state
                let! incumbent = requireSameIncumbency target road

                return
                    { Bound = target.Bound
                      SessionId = target.SessionId
                      RoadId = target.RoadId
                      View = road
                      Authority = road.AuthorityRevision
                      Incumbent = incumbent }
            }
        with _ ->
            Error(text Path.FinishFailed)

    let private refreshedOrRaise (scope: ToolRuntimeScope) (context: HostToolContext) refreshResult =
        match refreshResult with
        | Ok refreshed -> refreshed
        | Error error ->
            scope.UnfreezeRetirement context.SessionId
            raise (InvalidOperationException error)

    let private executePrepared (scope: ToolRuntimeScope) (context: HostToolContext) (target: FreezeTarget) =
        task {
            try
                scope.TryFreezeRetirement(context.SessionId, target.Incumbent) |> ignore
                let refreshed = refreshAfterFreeze context target |> refreshedOrRaise scope context
                let! outcome = runFrozen scope context refreshed
                return finishOutcome scope context outcome
            with ex ->
                unfreezeUnlessRetired scope context
                return raise ex
        }

    let private execute (scope: ToolRuntimeScope) (_: HostToolArguments) (context: HostToolContext) =
        match bindInvocation scope context |> Result.bind (prepareTarget context) with
        | Ok target -> executePrepared scope context target
        | Error error -> task { return raise (InvalidOperationException error) }

    let description () = text Path.Description

    let admission =
        ToolAdmission.OfficeRole(fun _ role -> OfficeCapability.isAllowed role ToolPermission.Finality)

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "deliver"
          Description = text Path.Description
          Arguments = []
          Admission = admission
          Execute = execute scope }
