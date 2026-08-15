namespace Wanxiangshu.Execution.Fission.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open System.Collections.Generic
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
open Wanxiangshu.Mission.Finality
open Wanxiangshu.OpenCode
open Wanxiangshu.Host
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
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

/// Host-side terminal bridge for same-participant Fission lanes. A physical lane
/// terminal is not a parent-visible agent completion. It materializes one keyed
/// lane LWR, and only the durable group convergence publishes a terminal on the
/// old logical owner's SessionId/completion cell.
module FissionHost =

    let private appendFission durable owner providerRun fact =
        task {
            match! AgentJournal.appendAgent (StreamId.Session owner) providerRun fact durable with
            | Ok _ -> return Ok()
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    let private readVerified (durable: AgentJournal) (blobRef: BlobRef) (digest: BlobDigest) =
        task {
            match! durable.Writer.BlobWriter.Read blobRef with
            | Ok text when HostDigest.sha256Hex text = BlobDigest.value digest -> return Ok text
            | Ok _ -> return Error "Fission aggregate blob digest mismatch"
            | Error error -> return Error error
        }

    let private completionDeliveredTo laneIndex completionId (group: FissionGroupProjection) =
        group.CompletionDeliveries
        |> Map.tryFind completionId
        |> Option.defaultValue Set.empty
        |> Set.contains laneIndex

    let private sharedInputsAccounted laneIndex (group: FissionGroupProjection) =
        group.PreFissionCompletionIds
        |> Set.forall (fun completionId -> completionDeliveredTo laneIndex completionId group)

    let private agentIdOfExternal (externalId: string) =
        if externalId.StartsWith("agent:", StringComparison.Ordinal) then
            Some(externalId.Substring("agent:".Length))
        else
            None

    let private laneHasOutstandingExternal durable laneIndex (group: FissionGroupProjection) =
        let handles = AgentJournal.handleProjection durable group.OwnerSessionId

        group.ExternalAffinities
        |> Map.exists (fun externalId affinity ->
            if affinity <> laneIndex then
                false
            else
                match agentIdOfExternal externalId with
                | None -> true
                | Some agentId ->
                    match HandleProjection.tryFind (HandleController.agentHandle agentId) handles with
                    | Some { Lifecycle = HandleLifecycle.Retired } -> false
                    | Some _ -> true
                    | None -> true)

    let private consumePreFissionAgents durable (group: FissionGroupProjection) =
        let rec consume completionIds =
            task {
                match completionIds with
                | [] -> return true
                | completionId :: rest ->
                    match agentIdOfExternal completionId with
                    | None -> return! consume rest
                    | Some agentId ->
                        let handle = HandleController.agentHandle agentId
                        let projection = AgentJournal.handleProjection durable group.OwnerSessionId

                        match HandleProjection.tryFind handle projection with
                        | Some { Lifecycle = HandleLifecycle.Retired } -> return! consume rest
                        | Some { Lifecycle = HandleLifecycle.CompletedAwaitingJoin _ }
                        | Some { Lifecycle = HandleLifecycle.Abandoned _ } ->
                            match! HandleController.consume durable group.OwnerSessionId handle with
                            | Ok _ -> return! consume rest
                            | Error _ ->
                                let after = AgentJournal.handleProjection durable group.OwnerSessionId

                                match HandleProjection.tryFind handle after with
                                | Some { Lifecycle = HandleLifecycle.Retired } -> return! consume rest
                                | _ -> return false
                        | Some { Lifecycle = HandleLifecycle.Active }
                        | None -> return false
            }

        group.PreFissionCompletionIds |> Set.toList |> List.sort |> consume

    let private aggregateWorkRecord durable (group: FissionGroupProjection) =
        task {
            match! readVerified durable group.OwnerWorkRecordRef group.OwnerWorkRecordDigest with
            | Error error -> return Error error
            | Ok ownerRecord ->
                let blocks = ResizeArray<string>()
                blocks.Add "[[fission_convergence]]"
                blocks.Add(sprintf "lane_count = %d" group.LaneCount)
                blocks.Add ""
                blocks.Add "[owner_before_fission]"
                blocks.Add ownerRecord

                let rec appendLaneWork laneIndex =
                    task {
                        if laneIndex >= group.LaneCount then
                            return Ok()
                        else
                            match Map.tryFind laneIndex group.LaneWork with
                            | None -> return Error(sprintf "missing lane work %d" laneIndex)
                            | Some(workRef, workDigest) ->
                                match! readVerified durable workRef workDigest with
                                | Error readError -> return Error readError
                                | Ok record ->
                                    blocks.Add ""
                                    blocks.Add(sprintf "[lane.%d]" laneIndex)
                                    blocks.Add record
                                    return! appendLaneWork (laneIndex + 1)
                    }

                let rec appendSharedCompletions completionIds =
                    task {
                        match completionIds with
                        | [] -> return Ok()
                        | completionId :: rest ->
                            match Map.tryFind completionId group.CapturedCompletions with
                            | None -> return Error("missing captured shared completion: " + completionId)
                            | Some(payloadRef, payloadDigest) ->
                                match! readVerified durable payloadRef payloadDigest with
                                | Error readError -> return Error readError
                                | Ok payload ->
                                    blocks.Add ""
                                    blocks.Add("[shared_completion.\"" + completionId.Replace("\"", "\\\"") + "\"]")
                                    blocks.Add payload
                                    return! appendSharedCompletions rest
                    }

                match! appendLaneWork 0 with
                | Error reason -> return Error reason
                | Ok() ->
                    match!
                        group.PreFissionCompletionIds
                        |> Set.toList
                        |> List.sort
                        |> appendSharedCompletions
                    with
                    | Error reason -> return Error reason
                    | Ok() -> return Ok(String.concat "\n" (blocks |> Seq.toList))
        }

    let private publishConverged
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        =
        task {
            match group.Terminal with
            | FissionGroupTerminal.Converged(_, providerRun, aggregateRef, aggregateDigest) ->
                match! readVerified durable aggregateRef aggregateDigest with
                | Error _ -> return false
                | Ok aggregate ->
                    let projections = (AgentJournal.snapshot durable).AgentProjections

                    let profile =
                        PromptAuthorityLedger.activeProfile group.OwnerSessionId projections
                        |> Option.orElseWith (fun () ->
                            PromptAuthorityLedger.lastAuthorityProfile group.OwnerSessionId projections)

                    match profile with
                    | None -> return false
                    | Some authority ->
                        eventPort.NotifyTerminal
                            group.OwnerSessionId
                            (TerminalOutcome.Completed
                                { SessionId = group.OwnerSessionId
                                  AuthorityRootUserMessageId = authority.AuthorityRootUserMessageId
                                  ProviderRun = providerRun
                                  Role = authority.CanonicalRole
                                  Directory = None
                                  TerminalText = aggregate
                                  TurnFormalText = aggregate })
                        |> ignore

                        FissionAdmission.releaseOwner group.OwnerSessionId
                        FissionRuntime.clearOwner group.OwnerSessionId
                        return true
            | _ -> return false
        }

    /// Attempt the single logical convergence. Safe to call from lane terminal,
    /// shared-completion delivery, or crash recovery. Completed publication is
    /// deduped by the shared Host event port on (owner SessionId, ProviderRun).
    let tryConverge (eventPort: IEventObservationPort) (durable: AgentJournal) (owner: SessionId) =
        task {
            match
                FissionProjection.tryLatestForOwner owner (AgentJournal.snapshot durable).AgentProjections.Fission
            with
            | None -> return false
            | Some group ->
                match group.Terminal with
                | FissionGroupTerminal.Failed _ -> return false
                | FissionGroupTerminal.Converged _ -> return! publishConverged eventPort durable group
                | FissionGroupTerminal.Open ->
                    let allLaneWork =
                        group.LaneWork.Count = group.LaneCount
                        && group.LaneProviderRuns.Count = group.LaneCount

                    let allShared =
                        group.PreFissionCompletionIds
                        |> Set.forall (fun completionId ->
                            let delivered =
                                group.CompletionDeliveries
                                |> Map.tryFind completionId
                                |> Option.defaultValue Set.empty

                            Map.containsKey completionId group.CapturedCompletions
                            && delivered = Set.ofList [ 0 .. group.LaneCount - 1 ])

                    if not allLaneWork || not allShared then
                        return false
                    else
                        let! preConsumed = consumePreFissionAgents durable group

                        if not preConsumed then
                            return false
                        else
                            match! aggregateWorkRecord durable group with
                            | Error _ -> return false
                            | Ok aggregate ->
                                match! durable.WriteBlob aggregate with
                                | Error _ -> return false
                                | Ok aggregateBlob ->
                                    let terminalIndex = group.LaneCount - 1

                                    match
                                        Map.tryFind terminalIndex group.LaneSessions,
                                        Map.tryFind terminalIndex group.LaneProviderRuns
                                    with
                                    | Some terminalLane, Some terminalRun ->
                                        match!
                                            appendFission
                                                durable
                                                owner
                                                (Some terminalRun)
                                                (FissionFact.FissionConverged
                                                    {| GroupId = group.GroupId
                                                       OwnerSessionId = owner
                                                       TerminalLaneSessionId = terminalLane
                                                       TerminalProviderRun = terminalRun
                                                       AggregateWorkRecordRef = aggregateBlob.BlobRef
                                                       AggregateWorkRecordDigest = aggregateBlob.BlobDigest |})
                                        with
                                        | Error _ -> return false
                                        | Ok() ->
                                            match
                                                FissionProjection.tryGroup
                                                    group.GroupId
                                                    (AgentJournal.snapshot durable).AgentProjections.Fission
                                            with
                                            | Some converged -> return! publishConverged eventPort durable converged
                                            | None -> return false
                                    | _ -> return false
        }

    let private capturePayload
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (completionId: string)
        (payload: string)
        =
        task {
            match Map.tryFind completionId group.CapturedCompletions with
            | Some existing -> return Ok existing
            | None ->
                match! durable.WriteBlob payload with
                | Error error -> return Error error
                | Ok blob ->
                    match!
                        appendFission
                            durable
                            group.OwnerSessionId
                            None
                            (FissionFact.FissionCompletionCaptured
                                {| GroupId = group.GroupId
                                   OwnerSessionId = group.OwnerSessionId
                                   CompletionId = completionId
                                   PayloadRef = blob.BlobRef
                                   PayloadDigest = blob.BlobDigest |})
                    with
                    | Error error -> return Error error
                    | Ok() -> return Ok(blob.BlobRef, blob.BlobDigest)
        }

    let private captureExistingBlob
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        completionId
        payloadRef
        payloadDigest
        =
        task {
            match Map.tryFind completionId group.CapturedCompletions with
            | Some existing -> return Ok existing
            | None ->
                match!
                    appendFission
                        durable
                        group.OwnerSessionId
                        None
                        (FissionFact.FissionCompletionCaptured
                            {| GroupId = group.GroupId
                               OwnerSessionId = group.OwnerSessionId
                               CompletionId = completionId
                               PayloadRef = payloadRef
                               PayloadDigest = payloadDigest |})
                with
                | Error error -> return Error error
                | Ok() -> return Ok(payloadRef, payloadDigest)
        }

    let private deliverCaptured
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (groupId: string)
        (completionId: string)
        =
        task {
            match FissionProjection.tryGroup groupId (AgentJournal.snapshot durable).AgentProjections.Fission with
            | None -> return ()
            | Some group ->
                match Map.tryFind completionId group.CapturedCompletions with
                | None -> return ()
                | Some(payloadRef, payloadDigest) ->
                    match! readVerified durable payloadRef payloadDigest with
                    | Error _ -> return ()
                    | Ok payload ->
                        let projections = (AgentJournal.snapshot durable).AgentProjections

                        let profile =
                            PromptAuthorityLedger.activeProfile group.OwnerSessionId projections
                            |> Option.orElseWith (fun () ->
                                PromptAuthorityLedger.lastAuthorityProfile group.OwnerSessionId projections)

                        match profile with
                        | None -> return ()
                        | Some authority ->
                            for laneIndex in [ 0 .. group.LaneCount - 1 ] do
                                let current =
                                    FissionProjection.tryGroup
                                        groupId
                                        (AgentJournal.snapshot durable).AgentProjections.Fission

                                match current with
                                | None -> ()
                                | Some now when completionDeliveredTo laneIndex completionId now -> ()
                                | Some now ->
                                    match Map.tryFind laneIndex now.LaneSessions with
                                    | None -> ()
                                    | Some laneSessionId ->
                                        if Map.containsKey laneIndex now.LaneWork then
                                            let! _ =
                                                appendFission
                                                    durable
                                                    now.OwnerSessionId
                                                    None
                                                    (FissionFact.FissionCompletionDelivered
                                                        {| GroupId = groupId
                                                           OwnerSessionId = now.OwnerSessionId
                                                           CompletionId = completionId
                                                           LaneIndex = laneIndex |})

                                            ()
                                        else
                                            let effectiveAgent = authority.SelectedAgent

                                            let prompt =
                                                String.concat
                                                    "\n"
                                                    [ "A completion that was already outstanding before Fission now belongs to every present of this same participant."
                                                      "Treat it as one shared completion fact, not as newly delegated work."
                                                      "completion_id = " + completionId
                                                      ""
                                                      payload ]

                                            let dispatcher = PromptDispatcher.forJournal durable

                                            match!
                                                dispatcher.SendContinuation
                                                    sessionPort
                                                    laneSessionId
                                                    prompt
                                                    PromptAuthority.ContinuationKind.FissionHandoff
                                                    authority
                                                    effectiveAgent
                                                    (directoryFor laneSessionId)
                                                    PromptDispatcher.AwaitMode.Detached
                                                    None
                                            with
                                            | Error _ -> ()
                                            | Ok _ ->
                                                let! _ =
                                                    appendFission
                                                        durable
                                                        now.OwnerSessionId
                                                        None
                                                        (FissionFact.FissionCompletionDelivered
                                                            {| GroupId = groupId
                                                               OwnerSessionId = now.OwnerSessionId
                                                               CompletionId = completionId
                                                               LaneIndex = laneIndex |})

                                                ()

                            let! _ = tryConverge eventPort durable group.OwnerSessionId
                            return ()
        }

    let private recoverPreCompletion
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (groupId: string)
        (completionId: string)
        =
        task {
            match FissionProjection.tryGroup groupId (AgentJournal.snapshot durable).AgentProjections.Fission with
            | None -> return ()
            | Some group when Map.containsKey completionId group.CapturedCompletions ->
                do! deliverCaptured sessionPort eventPort durable directoryFor groupId completionId
            | Some group ->
                match agentIdOfExternal completionId with
                | None -> return ()
                | Some agentId ->
                    let record =
                        AgentJournal.handleProjection durable group.OwnerSessionId
                        |> HandleProjection.tryFind (HandleController.agentHandle agentId)

                    match record with
                    | Some({ Lifecycle = HandleLifecycle.CompletedAwaitingJoin _ } as handle) ->
                        match! HandleCompletionCodec.tryReadBody durable handle with
                        | Ok(Some _, Some payloadRef, Some payloadDigest) ->
                            match! captureExistingBlob durable group completionId payloadRef payloadDigest with
                            | Error _ -> return ()
                            | Ok _ ->
                                do! deliverCaptured sessionPort eventPort durable directoryFor groupId completionId
                        | _ -> return ()
                    | Some { Lifecycle = HandleLifecycle.Abandoned reason } ->
                        let payload = sprintf "status = abandoned\nreason = %A" reason

                        match! capturePayload durable group completionId payload with
                        | Error _ -> return ()
                        | Ok _ -> do! deliverCaptured sessionPort eventPort durable directoryFor groupId completionId
                    | _ -> return ()
        }

    /// Rebuild process-local lane aliases and finish any durable Fission work that
    /// survived a plugin/process restart. Session discovery is never heuristic:
    /// every lane id comes from FissionAdmitted.
    let recoverGroups
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (registerOwned: SessionId -> unit)
        (bindActiveRun: SessionId -> Role -> string option -> unit)
        (directoryFor: SessionId -> string option)
        =
        task {
            let state = (AgentJournal.snapshot durable).AgentProjections.Fission

            for KeyValue(_, group) in state.Groups do
                match group.Terminal with
                | FissionGroupTerminal.Failed _ -> ()
                | FissionGroupTerminal.Converged _ ->
                    let! _ = tryConverge eventPort durable group.OwnerSessionId
                    ()
                | FissionGroupTerminal.Open ->
                    FissionAdmission.restoreActiveOwner group.OwnerSessionId

                    let profile =
                        PromptAuthorityLedger.activeProfile
                            group.OwnerSessionId
                            (AgentJournal.snapshot durable).AgentProjections
                        |> Option.orElseWith (fun () ->
                            PromptAuthorityLedger.lastAuthorityProfile
                                group.OwnerSessionId
                                (AgentJournal.snapshot durable).AgentProjections)

                    for KeyValue(laneIndex, laneSessionId) in group.LaneSessions do
                        FissionRuntime.bindLane
                            group.GroupId
                            group.OwnerSessionId
                            laneIndex
                            group.LaneCount
                            laneSessionId

                        registerOwned laneSessionId

                        profile
                        |> Option.iter (fun authority ->
                            bindActiveRun laneSessionId authority.CanonicalRole (directoryFor laneSessionId))

                    // The old physical present must remain retired even when the
                    // process died between durable admission and Host abort.
                    let! _ = sessionPort.InterruptSessionOnly group.OwnerSessionId

                    for completionId in group.PreFissionCompletionIds |> Set.toList do
                        do! recoverPreCompletion sessionPort eventPort durable directoryFor group.GroupId completionId

                        match agentIdOfExternal completionId with
                        | None -> ()
                        | Some agentId ->
                            let handle =
                                AgentJournal.handleProjection durable group.OwnerSessionId
                                |> HandleProjection.tryFind (HandleController.agentHandle agentId)

                            match handle with
                            | Some { Lifecycle = HandleLifecycle.Active
                                     ChildSessionId = childSessionId } ->
                                let subscription =
                                    eventPort.SubscribeTerminalListener(fun sessionId outcome ->
                                        if sessionId = childSessionId then
                                            match outcome with
                                            | TerminalOutcome.Aborted _ -> ()
                                            | TerminalOutcome.Failed error ->
                                                task {
                                                    match
                                                        FissionProjection.tryGroup
                                                            group.GroupId
                                                            (AgentJournal.snapshot durable).AgentProjections.Fission
                                                    with
                                                    | None -> ()
                                                    | Some current ->
                                                        let payload = "status = failed\nerror = " + error

                                                        let! captured =
                                                            capturePayload durable current completionId payload

                                                        match captured with
                                                        | Error _ -> ()
                                                        | Ok _ ->
                                                            do!
                                                                deliverCaptured
                                                                    sessionPort
                                                                    eventPort
                                                                    durable
                                                                    directoryFor
                                                                    group.GroupId
                                                                    completionId
                                                }
                                                |> ignore
                                            | TerminalOutcome.Completed terminal ->
                                                task {
                                                    let! workRecord =
                                                        LifecycleWorkRecordProjection.lifecycleWorkRecord
                                                            (Some durable)
                                                            childSessionId
                                                            false

                                                    let payload =
                                                        workRecord
                                                        |> Option.filter (String.IsNullOrWhiteSpace >> not)
                                                        |> Option.defaultValue terminal.TerminalText

                                                    match
                                                        FissionProjection.tryGroup
                                                            group.GroupId
                                                            (AgentJournal.snapshot durable).AgentProjections.Fission
                                                    with
                                                    | None -> ()
                                                    | Some current ->
                                                        let! captured =
                                                            capturePayload durable current completionId payload

                                                        match captured with
                                                        | Error _ -> ()
                                                        | Ok _ ->
                                                            do!
                                                                deliverCaptured
                                                                    sessionPort
                                                                    eventPort
                                                                    durable
                                                                    directoryFor
                                                                    group.GroupId
                                                                    completionId
                                                }
                                                |> ignore)

                                FissionRuntime.trackGroupResource group.GroupId subscription
                            | _ -> ()

                    let! _ = tryConverge eventPort durable group.OwnerSessionId
                    ()
        }

    let private failGroup
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        reason
        =
        task {
            match group.Terminal with
            | FissionGroupTerminal.Open ->
                match!
                    appendFission
                        durable
                        group.OwnerSessionId
                        None
                        (FissionFact.FissionFailed
                            {| GroupId = group.GroupId
                               OwnerSessionId = group.OwnerSessionId
                               Reason = reason |})
                with
                | Error _ -> return ()
                | Ok() ->
                    for KeyValue(_, laneSessionId) in group.LaneSessions do
                        let! _ = sessionPort.AbortSession laneSessionId
                        ()

                    eventPort.NotifyTerminal group.OwnerSessionId (TerminalOutcome.Failed reason)
                    |> ignore

                    FissionAdmission.releaseOwner group.OwnerSessionId
                    FissionRuntime.clearOwner group.OwnerSessionId
            | _ -> ()
        }

    /// Returns true when this turn belongs to Fission and its terminal semantics
    /// were consumed here. Retired owner sessions are absorbed; non-terminal lane
    /// turns return false so ordinary repair/recovery behavior still runs.
    let observeLaneTurn
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (turn: ReconciledTurn)
        : Task<bool> =
        task {
            let isOwnerReplaced =
                FissionRuntime.isSilentInterrupt turn.SessionId
                || (journal
                    |> Option.exists (fun durable ->
                        FissionProjection.tryActiveForOwner
                            turn.SessionId
                            (AgentJournal.snapshot durable).AgentProjections.Fission
                        |> Option.isSome))

            match isOwnerReplaced, journal with
            | true, _ ->
                // Old caller physical present was replaced and retired by Fission.
                // Absorb all turn observations silently; do not cascade or continue.
                return true
            | false, None -> return false
            | false, Some durable ->
                match
                    FissionProjection.tryMembershipOfLane
                        turn.SessionId
                        (AgentJournal.snapshot durable).AgentProjections.Fission
                with
                | None -> return false
                | Some(group, laneIndex) ->
                    match turn.Outcome with
                    | ReconcileProgram.TurnInProgress
                    | ReconcileProgram.TurnNeedsContinuation _ -> return false
                    | ReconcileProgram.TurnFailed _ ->
                        // Provider-attempt failure remains lane-local and follows the
                        // ordinary A/A/B/B recovery path. Fission does not turn one
                        // failed attempt into group failure.
                        return false
                    | ReconcileProgram.TurnAborted reason ->
                        do! failGroup sessionPort eventPort durable group reason
                        return true
                    | ReconcileProgram.TurnCompleted ->
                        match group.Terminal with
                        | FissionGroupTerminal.Converged _
                        | FissionGroupTerminal.Failed _ -> return true
                        | FissionGroupTerminal.Open ->
                            if laneHasOutstandingExternal durable laneIndex group then
                                let! _ =
                                    HostJoinGuard.nudge
                                        sessionPort
                                        journal
                                        joinGuardNudges
                                        turn.SessionId
                                        turn.Directory

                                return true
                            elif not (sharedInputsAccounted laneIndex group) then
                                // The shared completion broadcaster will send a same-run
                                // continuation when the missing fact arrives.
                                return true
                            else
                                match!
                                    LifecycleWorkRecordProjection.lifecycleWorkRecord (Some durable) turn.SessionId true
                                with
                                | None ->
                                    do!
                                        failGroup
                                            sessionPort
                                            eventPort
                                            durable
                                            group
                                            "Fission lane completed without canonical Lifecycle Work Record"

                                    return true
                                | Some workRecord ->
                                    match! durable.WriteBlob workRecord with
                                    | Error error ->
                                        do! failGroup sessionPort eventPort durable group error
                                        return true
                                    | Ok workBlob ->
                                        match!
                                            appendFission
                                                durable
                                                group.OwnerSessionId
                                                (Some turn.ProviderRun)
                                                (FissionFact.FissionLaneMaterialized
                                                    {| GroupId = group.GroupId
                                                       OwnerSessionId = group.OwnerSessionId
                                                       LaneIndex = laneIndex
                                                       LaneSessionId = turn.SessionId
                                                       ProviderRun = turn.ProviderRun
                                                       WorkRecordRef = workBlob.BlobRef
                                                       WorkRecordDigest = workBlob.BlobDigest |})
                                        with
                                        | Error error ->
                                            do! failGroup sessionPort eventPort durable group error
                                            return true
                                        | Ok() ->
                                            let! _ = tryConverge eventPort durable group.OwnerSessionId
                                            return true
        }
