namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open ToolHostCodec

/// Same-participant physical replacement. Fission never calls the Host session
/// fork endpoint: it creates fresh sibling sessions, starts them from the
/// canonical owner LWR + exact lane input, then physically interrupts the old
/// present without terminating the logical owner.
module FissionTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/fission/description"

        [<Literal>]
        let TooFew = "tool/fission/too-few"

        [<Literal>]
        let Capacity = "tool/fission/capacity"

        [<Literal>]
        let AlreadyActive = "tool/fission/already-active"

        [<Literal>]
        let Unavailable = "tool/fission/unavailable"

        [<Literal>]
        let Started = "tool/fission/started"

        [<Literal>]
        let SharedCompletion = "tool/fission/shared-completion"

    let private languageOf (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private consequence language path =
        tomlObjectWithInstructions (ProviderProse.instructionLines language path Map.empty) []

    let private appendFission
        (durable: AgentJournal)
        (owner: SessionId)
        (providerRun: ProviderRunIdentity option)
        (fact: AgentFact)
        =
        task {
            match! AgentJournal.appendAgent (StreamId.Session owner) providerRun fact durable with
            | Ok _ -> return Ok()
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    let private groupIdFor owner toolCallId =
        let identity =
            SessionId.value owner + "\u001f" + ToolCallId.value toolCallId
            |> HostDigest.sha256Hex

        "fission-" + identity.Substring(0, 24)

    let private currentEffectiveAgent (profile: PromptAuthority.AuthorityExecutionProfile) (ctx: HostToolContext) =
        match ctx.Agent with
        | Some agent when agent = profile.SelectedAgent || agent = profile.PeerAgent -> agent
        | _ -> profile.SelectedAgent

    let private deliveryPrompt owner completionId payload =
        ProviderProse.render
            (ProviderLanguageBinding.ensureRoot owner)
            Path.SharedCompletion
            (Map [ "completion_id", completionId; "payload", payload ])

    let private capturedGroup (durable: AgentJournal) groupId =
        FissionProjection.tryGroup groupId (AgentJournal.snapshot durable).AgentProjections.Fission

    let private appendDelivery durable owner providerRun groupId completionId laneIndex =
        appendFission
            durable
            owner
            providerRun
            (FissionFact.FissionCompletionDelivered
                {| GroupId = groupId
                   OwnerSessionId = owner
                   CompletionId = completionId
                   LaneIndex = laneIndex |})

    /// Capture exactly one canonical payload for a pre-Fission completion, then
    /// account/deliver that same payload once per lane. Closed lanes are accounted
    /// through the durable group closure; live lanes also receive a same-run
    /// continuation. No per-lane WorkRecord copy is created.
    let private captureAndBroadcast
        (scope: ToolRuntimeScope)
        (durable: AgentJournal)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (effectiveAgent: string)
        (groupId: string)
        (owner: SessionId)
        (lanes: FissionStartedLane list)
        (completionId: string)
        (payload: string)
        =
        task {
            if not (FissionRuntime.tryBeginDelivery groupId completionId -1) then
                return ()
            else
                try
                    let captured =
                        capturedGroup durable groupId
                        |> Option.bind (fun group -> Map.tryFind completionId group.CapturedCompletions)

                    match captured with
                    | None ->
                        match! durable.WriteBlob payload with
                        | Error _ -> return ()
                        | Ok blob ->
                            match!
                                appendFission
                                    durable
                                    owner
                                    None
                                    (FissionFact.FissionCompletionCaptured
                                        {| GroupId = groupId
                                           OwnerSessionId = owner
                                           CompletionId = completionId
                                           PayloadRef = blob.BlobRef
                                           PayloadDigest = blob.BlobDigest |})
                            with
                            | Error _ -> return ()
                            | Ok() -> ()
                    | Some _ -> ()

                    for lane in lanes |> List.sortBy (fun lane -> lane.Index) do
                        let alreadyDelivered, laneClosed =
                            match capturedGroup durable groupId with
                            | None -> true, true
                            | Some group ->
                                let delivered =
                                    group.CompletionDeliveries
                                    |> Map.tryFind completionId
                                    |> Option.defaultValue Set.empty
                                    |> Set.contains lane.Index

                                delivered, Map.containsKey lane.Index group.LaneWork

                        if not alreadyDelivered && FissionRuntime.tryBeginDelivery groupId completionId lane.Index then
                            try
                                if laneClosed then
                                    let! _ = appendDelivery durable owner None groupId completionId lane.Index
                                    ()
                                else
                                    let dispatcher = PromptDispatcher.forJournal durable

                                    match!
                                        dispatcher.SendContinuation
                                            scope.Sessions
                                            lane.SessionId
                                            (deliveryPrompt owner completionId payload)
                                            PromptAuthority.ContinuationKind.FissionHandoff
                                            profile
                                            effectiveAgent
                                            (scope.DirectoryFor(SessionId.value lane.SessionId))
                                            PromptDispatcher.AwaitMode.Detached
                                            None
                                    with
                                    | Error _ -> ()
                                    | Ok _ ->
                                        let! _ = appendDelivery durable owner None groupId completionId lane.Index
                                        ()
                            finally
                                FissionRuntime.endDelivery groupId completionId lane.Index

                    match scope.EventPort with
                    | None -> ()
                    | Some eventPort ->
                        let _, beforeRevision = durable.SnapshotWithRevision
                        let! converged = FissionHost.tryConverge eventPort durable owner

                        if not converged then
                            let readyExceptForRetirement =
                                match capturedGroup durable groupId with
                                | Some group ->
                                    group.LaneWork.Count = group.LaneCount
                                    && group.PreFissionCompletionIds
                                       |> Set.forall (fun id ->
                                           let delivered =
                                               group.CompletionDeliveries
                                               |> Map.tryFind id
                                               |> Option.defaultValue Set.empty

                                           Map.containsKey id group.CapturedCompletions
                                           && delivered = Set.ofList [ 0 .. group.LaneCount - 1 ])
                                | None -> false

                            if readyExceptForRetirement then
                                let! _ = durable.AwaitChangeFrom beforeRevision
                                let! _ = FissionHost.tryConverge eventPort durable owner
                                ()
                finally
                    FissionRuntime.endDelivery groupId completionId -1
        }

    let private installPreFissionBroadcasts
        (scope: ToolRuntimeScope)
        (durable: AgentJournal)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (effectiveAgent: string)
        (groupId: string)
        (owner: SessionId)
        (lanes: FissionStartedLane list)
        (preAgents: (string * SessionId) list)
        (prePtys: string list)
        (ownerRuntime: HostForkRuntime)
        =
        match scope.EventPort with
        | None -> ()
        | Some eventPort ->
            for agentId, childId in preAgents do
                let completionId = FissionExternalId.agent agentId

                let subscription =
                    eventPort.SubscribeTerminalListener(fun sessionId outcome ->
                        if sessionId = childId then
                            match outcome with
                            | TerminalOutcome.Aborted _ -> ()
                            | TerminalOutcome.Failed error ->
                                captureAndBroadcast
                                    scope
                                    durable
                                    profile
                                    effectiveAgent
                                    groupId
                                    owner
                                    lanes
                                    completionId
                                    (String.concat "\n" [ "status=failed"; "error=" + error ])
                                |> ignore
                            | TerminalOutcome.Completed terminal ->
                                task {
                                    let! workRecord = scope.ChildWorkRecordFor(SessionId.value childId)

                                    let payload =
                                        match workRecord with
                                        | Some record when not (String.IsNullOrWhiteSpace record) -> record
                                        | _ -> terminal.TerminalText

                                    do!
                                        captureAndBroadcast
                                            scope
                                            durable
                                            profile
                                            effectiveAgent
                                            groupId
                                            owner
                                            lanes
                                            completionId
                                            payload
                                }
                                |> ignore)

                FissionRuntime.trackGroupResource groupId subscription

            if not (List.isEmpty prePtys) then
                let wanted = Set.ofList prePtys

                let subscription =
                    ownerRuntime.SubscribePtyCompletion(fun item ->
                        let ptyId = PtyJoinItem.ptyId item

                        if Set.contains ptyId wanted then
                            let payload =
                                match item with
                                | PtyJoinItem.PtyExited exit -> exit.Outcome
                                | PtyJoinItem.PtyFailed failure ->
                                    String.concat "\n" [ "status=failed"; "code=" + failure.Code; "error=" + failure.Message ]
                                | PtyJoinItem.PtyAborted aborted ->
                                    String.concat "\n" [ "status=aborted"; "code=" + aborted.Code; "error=" + aborted.Message ]

                            captureAndBroadcast
                                scope
                                durable
                                profile
                                effectiveAgent
                                groupId
                                owner
                                lanes
                                (FissionExternalId.pty ptyId)
                                payload
                            |> ignore)

                FissionRuntime.trackGroupResource groupId subscription

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let language = languageOf ctx
            let prompts = args.Text "prompts"

            match FissionPrompt.parse prompts with
            | Error FissionRejectReason.TooFewLanes
            | Error(FissionRejectReason.EmptyLanePrompt _) -> return consequence language Path.TooFew
            | Error _ -> return consequence language Path.Unavailable
            | Ok parsed ->
                match ctx.ToolCallId, scope.Journal with
                | None, _
                | _, None -> return consequence language Path.Unavailable
                | Some toolCallId, Some durable when String.IsNullOrWhiteSpace ctx.SessionId ->
                    return consequence language Path.Unavailable
                | Some toolCallId, Some durable ->
                    let caller = SessionId.create ctx.SessionId
                    let owner = scope.LogicalOwnerFor caller
                    let fissionState = (AgentJournal.snapshot durable).AgentProjections.Fission

                    if FissionRuntime.tryLane caller |> Option.isSome then
                        return consequence language Path.AlreadyActive
                    elif FissionProjection.tryActiveForOwner owner fissionState |> Option.isSome then
                        return consequence language Path.AlreadyActive
                    else
                        match scope.ActiveProfileFor owner, scope.RuntimeFor ctx with
                        | None, _
                        | _, Error _ -> return consequence language Path.Unavailable
                        | Some profile, Ok ownerRuntime ->
                            let effectiveAgent = currentEffectiveAgent profile ctx
                            let groupId = groupIdFor owner toolCallId
                            let directory = scope.DirectoryFor ctx.SessionId |> Option.orElse scope.WorkspaceDirectory
                            let preAgents = ownerRuntime.SnapshotOutstandingAgentRuns()
                            let prePtys = ownerRuntime.SnapshotOutstandingPtyRuns()

                            if (not (List.isEmpty preAgents) || not (List.isEmpty prePtys)) && scope.EventPort.IsNone then
                                return consequence language Path.Unavailable
                            else
                                let preCompletionIds =
                                    (preAgents |> List.map (fst >> FissionExternalId.agent))
                                    @ (prePtys |> List.map FissionExternalId.pty)

                                let deps: FissionAdmissionDependencies =
                                    { ParentOf = scope.Sessions.TryGetParentSession
                                      OwnerWorkRecord =
                                        (fun sessionId ->
                                            task {
                                                match! scope.ParentWorkRecordFor(SessionId.value sessionId) with
                                                | Some record when not (String.IsNullOrWhiteSpace record) -> return Ok record
                                                | _ -> return Error "lifecycle_work_record_unavailable"
                                            })
                                      CreateLane =
                                        (fun logicalOwner physicalParent lane ->
                                            task {
                                                match!
                                                    scope.Sessions.CreateSiblingSession(
                                                        logicalOwner,
                                                        physicalParent,
                                                        { Title = Some(sprintf "Fission lane %d/%d" (lane.Index + 1) parsed.Count)
                                                          Agent = Some effectiveAgent
                                                          Directory = directory }
                                                    )
                                                with
                                                | Error error -> return Error error
                                                | Ok laneId ->
                                                    scope.RegisterPhysicalParent(laneId, physicalParent)
                                                    directory
                                                    |> Option.iter (fun path ->
                                                        scope.RegisterDirectory(SessionId.value laneId, path))

                                                    FissionRuntime.bindLane groupId owner lane.Index parsed.Count laneId
                                                    return Ok laneId
                                            })
                                      StartLane =
                                        (fun laneId startup ->
                                            task {
                                                scope.RunStarted laneId profile.CanonicalRole directory
                                                do! XTraceCapture.captureOpening scope.Journal laneId startup []

                                                let dispatcher = PromptDispatcher.forJournal durable

                                                match!
                                                    dispatcher.SendContinuation
                                                        scope.Sessions
                                                        laneId
                                                        startup
                                                        PromptAuthority.ContinuationKind.FissionHandoff
                                                        profile
                                                        effectiveAgent
                                                        directory
                                                        PromptDispatcher.AwaitMode.Detached
                                                        None
                                                with
                                                | Ok _ -> return Ok()
                                                | Error error -> return Error error
                                            })
                                      AbortLane =
                                        (fun laneId ->
                                            task {
                                                FissionRuntime.unbindLane laneId
                                                SessionExecutionBinding.drop laneId
                                                let! _ = scope.Sessions.AbortSession laneId
                                                return ()
                                            }
                                            :> Task)
                                      SilentInterruptOwner =
                                        (fun sessionId ->
                                            task {
                                                FissionRuntime.markSilentInterrupt sessionId

                                                match! scope.Sessions.InterruptSessionOnly sessionId with
                                                | Ok() -> return Ok()
                                                | Error error ->
                                                    FissionRuntime.clearSilentInterrupt sessionId
                                                    return Error error
                                            }) }

                                let hooks: FissionAdmissionHooks =
                                    { OnLanesCreated =
                                        (fun logicalOwner physicalParent ownerWorkRecord created ->
                                            task {
                                                match! durable.WriteBlob ownerWorkRecord with
                                                | Error error -> return Error error
                                                | Ok blob ->
                                                    let ordered = created |> List.sortBy (fun lane -> lane.Index)

                                                    return!
                                                        appendFission
                                                            durable
                                                            logicalOwner
                                                            ctx.ProviderRunId
                                                            (FissionFact.FissionAdmitted
                                                                {| GroupId = groupId
                                                                   OwnerSessionId = logicalOwner
                                                                   ParentSessionId = physicalParent
                                                                   OriginToolCallId = toolCallId
                                                                   LaneCount = parsed.Count
                                                                   LaneSessions = ordered |> List.map (fun lane -> lane.SessionId)
                                                                   LanePrompts = ordered |> List.map (fun lane -> lane.Prompt)
                                                                   OwnerWorkRecordRef = blob.BlobRef
                                                                   OwnerWorkRecordDigest = blob.BlobDigest
                                                                   PreFissionCompletionIds = preCompletionIds |})
                                            })
                                      OnFailed =
                                        (fun logicalOwner reason ->
                                            task {
                                                let! _ =
                                                    appendFission
                                                        durable
                                                        logicalOwner
                                                        ctx.ProviderRunId
                                                        (FissionFact.FissionFailed
                                                            {| GroupId = groupId
                                                               OwnerSessionId = logicalOwner
                                                               Reason = reason |})

                                                return ()
                                            }
                                            :> Task) }

                                let admissionRuntime = FissionAdmission.createWithHooks deps hooks

                                match! FissionAdmission.admit admissionRuntime owner parsed with
                                | Error FissionRejectReason.AlreadyFissioned ->
                                    return consequence language Path.AlreadyActive
                                | Error FissionRejectReason.CapacityExceeded ->
                                    return consequence language Path.Capacity
                                | Error _ -> return consequence language Path.Unavailable
                                | Ok admission ->
                                    installPreFissionBroadcasts
                                        scope
                                        durable
                                        profile
                                        effectiveAgent
                                        groupId
                                        owner
                                        admission.Lanes
                                        preAgents
                                        prePtys
                                        ownerRuntime

                                    return
                                        tomlObject
                                            [ "status", TString "fissioned"
                                              "lane_count", TInt parsed.Count ]
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fission"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = [ "prompts", ToolHostCodec.stringSchema factory ]
          Execute = execute scope }
