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

    let private handleStillOutstanding handles agentId =
        match HandleProjection.tryFind (HandleController.agentHandle agentId) handles with
        | Some { Lifecycle = HandleLifecycle.Retired } -> false
        | Some _
        | None -> true

    let private externalStillOutstanding handles externalId =
        match agentIdOfExternal externalId with
        | None -> true
        | Some agentId -> handleStillOutstanding handles agentId

    let private affinityOutstanding handles laneIndex externalId affinity =
        affinity = laneIndex && externalStillOutstanding handles externalId

    let private laneHasOutstandingExternal durable laneIndex (group: FissionGroupProjection) =
        let handles = AgentJournal.handleProjection durable group.OwnerSessionId

        group.ExternalAffinities
        |> Map.exists (fun externalId affinity -> affinityOutstanding handles laneIndex externalId affinity)

    let private isHandleRetired durable ownerSessionId handle =
        match HandleProjection.tryFind handle (AgentJournal.handleProjection durable ownerSessionId) with
        | Some { Lifecycle = HandleLifecycle.Retired } -> true
        | _ -> false

    let private ensureRetiredAfterConsume durable ownerSessionId handle =
        task {
            match! HandleController.consume durable ownerSessionId handle with
            | Ok _ -> return true
            | Error _ -> return isHandleRetired durable ownerSessionId handle
        }

    let private tryConsumeCompletedHandle durable ownerSessionId handle =
        match HandleProjection.tryFind handle (AgentJournal.handleProjection durable ownerSessionId) with
        | Some { Lifecycle = HandleLifecycle.Retired } -> task { return true }
        | Some { Lifecycle = HandleLifecycle.CompletedAwaitingJoin _ }
        | Some { Lifecycle = HandleLifecycle.Abandoned _ } -> ensureRetiredAfterConsume durable ownerSessionId handle
        | Some { Lifecycle = HandleLifecycle.Active }
        | None -> task { return false }

    let private consumeOnePreFission durable (group: FissionGroupProjection) completionId =
        match agentIdOfExternal completionId with
        | None -> task { return true }
        | Some agentId -> tryConsumeCompletedHandle durable group.OwnerSessionId (HandleController.agentHandle agentId)

    let private continuePreFissionConsume ok rest consume =
        if ok then consume rest else task { return false }

    let private consumePreFissionAgents durable (group: FissionGroupProjection) =
        let rec consume completionIds =
            task {
                match completionIds with
                | [] -> return true
                | completionId :: rest ->
                    let! ok = consumeOnePreFission durable group completionId
                    return! continuePreFissionConsume ok rest consume
            }

        group.PreFissionCompletionIds |> Set.toList |> List.sort |> consume

    let private appendOneLaneWork
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (blocks: ResizeArray<string>)
        laneIndex
        =
        taskResult {
            match Map.tryFind laneIndex group.LaneWork with
            | None -> return! Error(sprintf "missing lane work %d" laneIndex)
            | Some(workRef, workDigest) ->
                let! record = readVerified durable workRef workDigest
                blocks.Add ""
                blocks.Add(sprintf "[lane.%d]" laneIndex)
                blocks.Add record
                return ()
        }

    let rec private appendLaneWorkBlocks
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (blocks: ResizeArray<string>)
        laneIndex
        =
        taskResult {
            if laneIndex >= group.LaneCount then
                return ()
            else
                do! appendOneLaneWork durable group blocks laneIndex
                return! appendLaneWorkBlocks durable group blocks (laneIndex + 1)
        }

    let private appendOneSharedCompletion
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (blocks: ResizeArray<string>)
        completionId
        =
        taskResult {
            match Map.tryFind completionId group.CapturedCompletions with
            | None -> return! Error("missing captured shared completion: " + completionId)
            | Some(payloadRef, payloadDigest) ->
                let! payload = readVerified durable payloadRef payloadDigest
                blocks.Add ""
                blocks.Add("[shared_completion.\"" + completionId.Replace("\"", "\\\"") + "\"]")
                blocks.Add payload
                return ()
        }

    let rec private appendSharedCompletionBlocks
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (blocks: ResizeArray<string>)
        completionIds
        =
        taskResult {
            match completionIds with
            | [] -> return ()
            | completionId :: rest ->
                do! appendOneSharedCompletion durable group blocks completionId
                return! appendSharedCompletionBlocks durable group blocks rest
        }

    let private aggregateWorkRecord (durable: AgentJournal) (group: FissionGroupProjection) =
        taskResult {
            let! ownerRecord = readVerified durable group.OwnerWorkRecordRef group.OwnerWorkRecordDigest
            // DSL-MUTABLE: algorithm-scratch — work record block accumulator
            let blocks = ResizeArray<string>()
            blocks.Add "[[fission_convergence]]"
            blocks.Add(sprintf "lane_count = %d" group.LaneCount)
            blocks.Add ""
            blocks.Add "[owner_before_fission]"
            blocks.Add ownerRecord
            do! appendLaneWorkBlocks durable group blocks 0

            do!
                group.PreFissionCompletionIds
                |> Set.toList
                |> List.sort
                |> appendSharedCompletionBlocks durable group blocks

            return String.concat "\n" (blocks |> Seq.toList)
        }

    let private ownerAuthority projections ownerSessionId =
        PromptAuthorityLedger.activeProfile ownerSessionId projections
        |> Option.orElseWith (fun () -> PromptAuthorityLedger.lastAuthorityProfile ownerSessionId projections)

    let private allLaneWorkPresent (group: FissionGroupProjection) =
        group.LaneWork.Count = group.LaneCount
        && group.LaneProviderRuns.Count = group.LaneCount

    let private allSharedCompletionsDelivered (group: FissionGroupProjection) =
        group.PreFissionCompletionIds
        |> Set.forall (fun completionId ->
            let delivered =
                group.CompletionDeliveries
                |> Map.tryFind completionId
                |> Option.defaultValue Set.empty

            Map.containsKey completionId group.CapturedCompletions
            && delivered = Set.ofList [ 0 .. group.LaneCount - 1 ])

    let private takeoverPrompt aggregate =
        String.concat
            "\n"
            [ "The Fission ring has collected every lane's canonical work record and returned the complete handoff to this final present."
              "Continue as the same logical participant. Integrate the handoff below and now produce the ordinary final report to your commissioner."
              "Do not report lane mechanics, physical session ids, or the internal Fission topology."
              ""
              "[fission_ring_handoff]"
              aggregate ]

    let private appendTakeoverStarted
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (aggregateBlob: BlobWriteReceipt)
        laneIndex
        laneSessionId
        physicalUserMessageId
        =
        appendFission
            durable
            group.OwnerSessionId
            None
            (FissionFact.FissionTakeoverStarted
                {| GroupId = group.GroupId
                   OwnerSessionId = group.OwnerSessionId
                   LaneIndex = laneIndex
                   LaneSessionId = laneSessionId
                   PhysicalUserMessageId = physicalUserMessageId
                   AggregateWorkRecordRef = aggregateBlob.BlobRef
                   AggregateWorkRecordDigest = aggregateBlob.BlobDigest |})

    let private sendTakeover
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (group: FissionGroupProjection)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (aggregateBlob: BlobWriteReceipt)
        aggregate
        laneIndex
        laneSessionId
        =
        task {
            let acceptedPhysicalId = TaskCompletionSource<PhysicalUserMessageId>()
            let dispatcher = PromptDispatcher.forJournal durable

            match!
                dispatcher.SendContinuation
                    sessionPort
                    laneSessionId
                    (takeoverPrompt aggregate)
                    PromptAuthority.ContinuationKind.FissionHandoff
                    authority
                    authority.SelectedAgent
                    (directoryFor laneSessionId)
                    PromptDispatcher.AwaitMode.Await
                    (Some(fun physicalId -> acceptedPhysicalId.TrySetResult physicalId |> ignore))
            with
            | Error _ -> return false
            | Ok _ ->
                let! physicalId = acceptedPhysicalId.Task

                match! appendTakeoverStarted durable group aggregateBlob laneIndex laneSessionId physicalId with
                | Error _ -> return false
                | Ok() -> return true
        }

    let private sendTakeoverWithAggregateBlob
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (group: FissionGroupProjection)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (aggregateBlob: BlobWriteReceipt)
        aggregate
        =
        let target =
            group.LastMaterializedLaneIndex
            |> Option.bind (fun laneIndex ->
                Map.tryFind laneIndex group.LaneSessions
                |> Option.map (fun laneSessionId -> laneIndex, laneSessionId))

        match target with
        | None -> task { return false }
        | Some(laneIndex, laneSessionId) ->
            sendTakeover
                sessionPort
                durable
                directoryFor
                group
                authority
                aggregateBlob
                aggregate
                laneIndex
                laneSessionId

    let private writeAggregateAndTakeover
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (group: FissionGroupProjection)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        aggregate
        =
        task {
            match! durable.WriteBlob aggregate with
            | Error _ -> return false
            | Ok aggregateBlob ->
                return!
                    sendTakeoverWithAggregateBlob
                        sessionPort
                        durable
                        directoryFor
                        group
                        authority
                        aggregateBlob
                        aggregate
        }

    let private startTakeoverWithAuthority
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (group: FissionGroupProjection)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        =
        task {
            match! aggregateWorkRecord durable group with
            | Error _ -> return false
            | Ok aggregate ->
                return! writeAggregateAndTakeover sessionPort durable directoryFor group authority aggregate
        }

    let private runClaimedTakeover
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (group: FissionGroupProjection)
        =
        task {
            try
                let projections = (AgentJournal.snapshot durable).AgentProjections

                return!
                    ownerAuthority projections group.OwnerSessionId
                    |> Option.map (startTakeoverWithAuthority sessionPort durable directoryFor group)
                    |> Option.defaultValue (task { return false })
            finally
                FissionRuntime.endTakeover group.GroupId
        }

    let private startTakeover
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (group: FissionGroupProjection)
        =
        if FissionRuntime.tryBeginTakeover group.GroupId then
            runClaimedTakeover sessionPort durable directoryFor group
        else
            task { return false }

    let private startTakeoverIfPreConsumed
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (group: FissionGroupProjection)
        preConsumed
        =
        if preConsumed then
            startTakeover sessionPort durable directoryFor group
        else
            task { return false }

    let private tryConvergeOpen
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (group: FissionGroupProjection)
        =
        task {
            if group.Takeover.IsSome then
                return true
            elif not (allLaneWorkPresent group) || not (allSharedCompletionsDelivered group) then
                return false
            else
                let! preConsumed = consumePreFissionAgents durable group
                return! startTakeoverIfPreConsumed sessionPort durable directoryFor group preConsumed
        }

    let private tryConvergeGroup
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (group: FissionGroupProjection)
        =
        match group.Terminal with
        | FissionGroupTerminal.Failed _ -> task { return false }
        | FissionGroupTerminal.Converged _ -> task { return true }
        | FissionGroupTerminal.Open -> tryConvergeOpen sessionPort durable directoryFor group

    /// Once every lane record and shared completion is accounted for, route the
    /// complete ring bundle back into the final physical present. The logical
    /// owner remains open until that continuation itself reaches an ordinary
    /// terminal turn.
    let tryConverge
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (owner: SessionId)
        =
        task {
            match
                FissionProjection.tryLatestForOwner owner (AgentJournal.snapshot durable).AgentProjections.Fission
            with
            | None -> return false
            | Some group -> return! tryConvergeGroup sessionPort durable directoryFor group
        }

    let private captureNewPayload
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        completionId
        (payload: string)
        =
        taskResult {
            let! blob = durable.WriteBlob payload

            do!
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

            return (blob.BlobRef, blob.BlobDigest)
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
            | None -> return! captureNewPayload durable group completionId payload
        }

    let private appendDeliveryFact (durable: AgentJournal) groupId ownerSessionId completionId laneIndex =
        appendFission
            durable
            ownerSessionId
            None
            (FissionFact.FissionCompletionDelivered
                {| GroupId = groupId
                   OwnerSessionId = ownerSessionId
                   CompletionId = completionId
                   LaneIndex = laneIndex |})

    let private sendSharedCompletionContinuation
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        laneSessionId
        completionId
        (payload: string)
        =
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

    let private sendThenMarkDelivery
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        groupId
        completionId
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (now: FissionGroupProjection)
        laneIndex
        laneSessionId
        (payload: string)
        =
        task {
            match!
                sendSharedCompletionContinuation
                    sessionPort
                    durable
                    directoryFor
                    authority
                    laneSessionId
                    completionId
                    payload
            with
            | Error _ -> return ()
            | Ok _ ->
                let! _ = appendDeliveryFact durable groupId now.OwnerSessionId completionId laneIndex
                return ()
        }

    let private deliverOrMarkLane
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        groupId
        completionId
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (now: FissionGroupProjection)
        laneIndex
        laneSessionId
        (payload: string)
        =
        if Map.containsKey laneIndex now.LaneWork then
            task {
                let! _ = appendDeliveryFact durable groupId now.OwnerSessionId completionId laneIndex
                return ()
            }
        else
            sendThenMarkDelivery
                sessionPort
                durable
                directoryFor
                groupId
                completionId
                authority
                now
                laneIndex
                laneSessionId
                payload

    let private deliverOneLaneSession
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        groupId
        completionId
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (now: FissionGroupProjection)
        laneIndex
        (payload: string)
        =
        match Map.tryFind laneIndex now.LaneSessions with
        | None -> task { return () }
        | Some laneSessionId ->
            deliverOrMarkLane
                sessionPort
                durable
                directoryFor
                groupId
                completionId
                authority
                now
                laneIndex
                laneSessionId
                payload

    let private deliverOneLane
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        groupId
        completionId
        laneIndex
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (payload: string)
        =
        let current =
            FissionProjection.tryGroup groupId (AgentJournal.snapshot durable).AgentProjections.Fission

        match current with
        | None -> task { return () }
        | Some now when completionDeliveredTo laneIndex completionId now -> task { return () }
        | Some now ->
            deliverOneLaneSession sessionPort durable directoryFor groupId completionId authority now laneIndex payload

    let private deliverAllLanes
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        groupId
        completionId
        (group: FissionGroupProjection)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (payload: string)
        =
        task {
            for laneIndex in [ 0 .. group.LaneCount - 1 ] do
                do! deliverOneLane sessionPort durable directoryFor groupId completionId laneIndex authority payload
        }

    let private deliverPayloadWithAuthority
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        groupId
        completionId
        (group: FissionGroupProjection)
        (payload: string)
        =
        let projections = (AgentJournal.snapshot durable).AgentProjections

        match ownerAuthority projections group.OwnerSessionId with
        | None -> task { return () }
        | Some authority ->
            task {
                do! deliverAllLanes sessionPort durable directoryFor groupId completionId group authority payload

                let! _ = tryConverge sessionPort durable directoryFor group.OwnerSessionId
                return ()
            }

    let private deliverVerifiedPayload
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        groupId
        completionId
        (group: FissionGroupProjection)
        payloadRef
        payloadDigest
        =
        task {
            match! readVerified durable payloadRef payloadDigest with
            | Error _ -> return ()
            | Ok payload ->
                return!
                    deliverPayloadWithAuthority
                        sessionPort
                        eventPort
                        durable
                        directoryFor
                        groupId
                        completionId
                        group
                        payload
        }

    let private deliverCapturedInGroup
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (directoryFor: SessionId -> string option)
        groupId
        (group: FissionGroupProjection)
        completionId
        =
        task {
            match Map.tryFind completionId group.CapturedCompletions with
            | None -> return ()
            | Some(payloadRef, payloadDigest) ->
                return!
                    deliverVerifiedPayload
                        sessionPort
                        eventPort
                        durable
                        directoryFor
                        groupId
                        completionId
                        group
                        payloadRef
                        payloadDigest
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
                return! deliverCapturedInGroup sessionPort eventPort durable directoryFor groupId group completionId
        }

    let private abortAllLaneSessions (sessionPort: ISessionHostPort) (group: FissionGroupProjection) =
        task {
            for KeyValue(_, laneSessionId) in group.LaneSessions do
                let! _ = sessionPort.AbortSession laneSessionId
                ()
        }

    let private failOpenGroup
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        reason
        =
        task {
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
                do! abortAllLaneSessions sessionPort group

                eventPort.NotifyTerminal group.OwnerSessionId (TerminalOutcome.Failed reason)
                |> ignore

                FissionAdmission.releaseOwner group.OwnerSessionId
                FissionRuntime.clearOwner group.OwnerSessionId
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
            | FissionGroupTerminal.Open -> do! failOpenGroup sessionPort eventPort durable group reason
            | _ -> ()
        }

    let private validateOwnerRunResult (result: AgentRunResult) =
        if result.IsValid then
            Ok result
        else
            Error "Fission takeover completed with empty terminal output"

    let private ownerRunResult (durable: AgentJournal) (group: FissionGroupProjection) (turn: ReconciledTurn) =
        let projections = (AgentJournal.snapshot durable).AgentProjections

        match ownerAuthority projections group.OwnerSessionId with
        | None -> Error "Fission takeover completed without owner authority"
        | Some authority ->
            { SessionId = group.OwnerSessionId
              AuthorityRootUserMessageId = authority.AuthorityRootUserMessageId
              ProviderRun = turn.ProviderRun
              Role = authority.CanonicalRole
              Directory = turn.Directory
              TerminalText = CompletedTurnClassifier.partsSessionText turn.Parts
              TurnFormalText = CompletedTurnClassifier.partsText turn.Parts }
            |> validateOwnerRunResult

    let private appendConvergedFromTakeover
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (takeover: FissionTakeoverProjection)
        (turn: ReconciledTurn)
        =
        appendFission
            durable
            group.OwnerSessionId
            (Some turn.ProviderRun)
            (FissionFact.FissionConverged
                {| GroupId = group.GroupId
                   OwnerSessionId = group.OwnerSessionId
                   TerminalLaneSessionId = turn.SessionId
                   TerminalProviderRun = turn.ProviderRun
                   AggregateWorkRecordRef = takeover.AggregateWorkRecordRef
                   AggregateWorkRecordDigest = takeover.AggregateWorkRecordDigest |})

    let private publishOwnerTakeover
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (turn: ReconciledTurn)
        (result: AgentRunResult)
        =
        task {
            do! XTraceCapture.captureTerminal (Some durable) turn

            eventPort.NotifyTerminal group.OwnerSessionId (TerminalOutcome.Completed result)
            |> ignore

            FissionAdmission.releaseOwner group.OwnerSessionId
            FissionRuntime.clearOwner group.OwnerSessionId
            return true
        }

    let private persistAndPublishTakeover
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (takeover: FissionTakeoverProjection)
        (turn: ReconciledTurn)
        (result: AgentRunResult)
        =
        task {
            match! appendConvergedFromTakeover durable group takeover turn with
            | Error error ->
                do! failGroup sessionPort eventPort durable group error
                return true
            | Ok() -> return! publishOwnerTakeover eventPort durable group turn result
        }

    let private completeTakeover
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (takeover: FissionTakeoverProjection)
        (turn: ReconciledTurn)
        =
        task {
            match ownerRunResult durable group turn with
            | Error error ->
                do! failGroup sessionPort eventPort durable group error
                return true
            | Ok result -> return! persistAndPublishTakeover sessionPort eventPort durable group takeover turn result
        }

    let private appendLaneMaterialized
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        laneIndex
        (turn: ReconciledTurn)
        (workBlob: BlobWriteReceipt)
        =
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

    let private afterLaneMaterializedAppend
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (directoryFor: SessionId -> string option)
        (outcome: Result<unit, string>)
        =
        task {
            match outcome with
            | Error error ->
                do! failGroup sessionPort eventPort durable group error
                return true
            | Ok() ->
                let! _ = tryConverge sessionPort durable directoryFor group.OwnerSessionId
                return true
        }

    let private directoryForTurn (turn: ReconciledTurn) laneSessionId =
        if laneSessionId = turn.SessionId then
            turn.Directory
        else
            None

    let private writeLaneWorkAndMaterialize
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        laneIndex
        (turn: ReconciledTurn)
        (workRecord: string)
        =
        task {
            match! durable.WriteBlob workRecord with
            | Error error ->
                do! failGroup sessionPort eventPort durable group error
                return true
            | Ok workBlob ->
                let! outcome = appendLaneMaterialized durable group laneIndex turn workBlob
                return! afterLaneMaterializedAppend sessionPort eventPort durable group (directoryForTurn turn) outcome
        }

    let private materializeCompletedLane
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        laneIndex
        (turn: ReconciledTurn)
        =
        task {
            match! LifecycleWorkRecordProjection.lifecycleWorkRecord (Some durable) turn.SessionId true with
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
                return! writeLaneWorkAndMaterialize sessionPort eventPort durable group laneIndex turn workRecord
        }

    let private observeOpenLaneCompletion
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        laneIndex
        (turn: ReconciledTurn)
        =
        task {
            if laneHasOutstandingExternal durable laneIndex group then
                let! _ = HostJoinGuard.nudge sessionPort journal joinGuardNudges turn.SessionId turn.Directory

                return true
            elif not (sharedInputsAccounted laneIndex group) then
                // The shared completion broadcaster will send a same-run
                // continuation when the missing fact arrives.
                return true
            else
                return! materializeCompletedLane sessionPort eventPort durable group laneIndex turn
        }

    let private observeCompletedLane
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        laneIndex
        (turn: ReconciledTurn)
        =
        match group.Terminal with
        | FissionGroupTerminal.Converged _
        | FissionGroupTerminal.Failed _ -> task { return true }
        | FissionGroupTerminal.Open ->
            observeOpenLaneCompletion sessionPort eventPort journal joinGuardNudges durable group laneIndex turn

    [<RequireQualifiedAccess>]
    type private TakeoverTurnDisposition =
        | Stale
        | Current

    let private classifyTakeoverTurn (takeover: FissionTakeoverProjection) (turn: ReconciledTurn) =
        if turn.SessionId <> takeover.LaneSessionId then
            TakeoverTurnDisposition.Stale
        elif turn.PhysicalUserMessageId <> takeover.PhysicalUserMessageId then
            TakeoverTurnDisposition.Stale
        else
            TakeoverTurnDisposition.Current

    let private observeCurrentTakeoverOutcome
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (takeover: FissionTakeoverProjection)
        (turn: ReconciledTurn)
        =
        match turn.Outcome with
        | ReconcileProgram.TurnInProgress
        | ReconcileProgram.TurnNeedsContinuation _
        | ReconcileProgram.TurnFailed _ -> task { return false }
        | ReconcileProgram.TurnAborted reason ->
            task {
                do! failGroup sessionPort eventPort durable group reason
                return true
            }
        | ReconcileProgram.TurnCompleted -> completeTakeover sessionPort eventPort durable group takeover turn

    let private observeTakeoverOutcome
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        (takeover: FissionTakeoverProjection)
        (turn: ReconciledTurn)
        =
        match classifyTakeoverTurn takeover turn with
        | TakeoverTurnDisposition.Stale ->
            // The handoff is already admitted. Any replay of an earlier slice
            // terminal is stale and cannot rematerialize or complete the owner.
            task { return true }
        | TakeoverTurnDisposition.Current ->
            observeCurrentTakeoverOutcome sessionPort eventPort durable group takeover turn

    let private observeOpenLaneOutcome
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        laneIndex
        (turn: ReconciledTurn)
        =
        match turn.Outcome with
        | ReconcileProgram.TurnInProgress
        | ReconcileProgram.TurnNeedsContinuation _ -> task { return false }
        | ReconcileProgram.TurnFailed _ ->
            // Provider-attempt failure remains lane-local and follows the
            // ordinary A/A/B/B recovery path. Fission does not turn one
            // failed attempt into group failure.
            task { return false }
        | ReconcileProgram.TurnAborted reason ->
            task {
                do! failGroup sessionPort eventPort durable group reason
                return true
            }
        | ReconcileProgram.TurnCompleted ->
            observeCompletedLane sessionPort eventPort journal joinGuardNudges durable group laneIndex turn

    let private observeLaneOutcome
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (durable: AgentJournal)
        (group: FissionGroupProjection)
        laneIndex
        (turn: ReconciledTurn)
        =
        match group.Terminal, group.Takeover with
        | FissionGroupTerminal.Converged _, _
        | FissionGroupTerminal.Failed _, _ -> task { return true }
        | FissionGroupTerminal.Open, Some takeover ->
            observeTakeoverOutcome sessionPort eventPort durable group takeover turn
        | FissionGroupTerminal.Open, None ->
            observeOpenLaneOutcome sessionPort eventPort journal joinGuardNudges durable group laneIndex turn

    let private observeDurableLaneTurn
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (durable: AgentJournal)
        (turn: ReconciledTurn)
        =
        match
            FissionProjection.tryMembershipOfLane
                turn.SessionId
                (AgentJournal.snapshot durable).AgentProjections.Fission
        with
        | None -> task { return false }
        | Some(group, laneIndex) ->
            observeLaneOutcome sessionPort eventPort journal joinGuardNudges durable group laneIndex turn

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
                return! observeDurableLaneTurn sessionPort eventPort journal joinGuardNudges durable turn
        }
