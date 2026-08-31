namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation

type internal CapacityStepDemand<'target> =
    { Sequence: int64
      SessionId: string
      PhysicalUserMessageId: string
      Target: 'target
      Fence: Set<string>
      TryOrdinary: 'target array -> bool
      Completion: TaskCompletionSource<unit> }

type internal ExecutionAdmissionDemand =
    { Sequence: int64
      SessionId: string
      PhysicalUserMessageId: string
      EffectiveAgent: string
      PreviousTarget: ModelRoutingTarget option
      Node: ExecutionAdmissionQueueNode }

[<RequireQualifiedAccess>]
type private EnqueueEvidence =
    | NewLogicalOperation
    | SameLogicalOperation of ExecutionAdmissionQueueNode
    | NewerPhysicalGeneration of ExecutionAdmissionDemand

[<RequireQualifiedAccess>]
type private QueueBoundDecision =
    | HasRoom
    | Full

module internal ModelCapacityQueue =
    [<Literal>]
    let ContractVersion = 1

    [<Literal>]
    let MaximumPendingDemands = 32

type internal CapacityStepDemandQueue<'target>() =
    let demands = ResizeArray<CapacityStepDemand<'target>>()
    // DSL-MUTABLE: resource
    let mutable nextCapacityDemandSequence = 0L

    member _.NextSequence() =
        nextCapacityDemandSequence <- nextCapacityDemandSequence + 1L
        nextCapacityDemandSequence

    member _.TryAdd(demand) =
        if demands.Count >= ModelCapacityQueue.MaximumPendingDemands then
            false
        else
            demands.Add demand
            true

    member _.Remove(demand) = demands.Remove demand
    member _.Clear() = demands.Clear()
    member _.Snapshot() = demands.ToArray()
    member _.Count = demands.Count

type internal ExecutionAdmissionQueue(owner: obj, counters: CapacityTransitionCounters) =
    let demands = ResizeArray<ExecutionAdmissionDemand>()
    /// DSL-cross-callback-proof: physical waiter — bounded exact-demand index for one-shot queue settlement.
    let bySession = Dictionary<string, ExecutionAdmissionDemand>()
    // DSL-MUTABLE: resource
    let mutable nextAdmissionDemandSequence = 0L
    let mutable nextNodeId = QueueNodeId.initial
    // DSL-MUTABLE: resource
    let mutable nextFence = QueueFence.initial

    let trySetException (completion: TaskCompletionSource<ExecutionAdmissionAcquisition>) (error: exn) =
        try
            completion.SetException error
            true
        with _ ->
            false

    let remove demand =
        let removed = demands.Remove demand

        match bySession.TryGetValue demand.SessionId with
        | true, current when obj.ReferenceEquals(current, demand) -> bySession.Remove demand.SessionId |> ignore
        | _ -> ()

        removed

    let complete outcome demand =
        if remove demand then
            AsyncSupport.trySetResult demand.Node.Completion outcome |> ignore
            true
        else
            false

    let knownNode (node: ExecutionAdmissionQueueNode) =
        obj.ReferenceEquals(node.Owner, owner)
        && (match bySession.TryGetValue node.SessionId with
            | true, demand ->
                obj.ReferenceEquals(demand.Node, node)
                && demand.Node.NodeId = node.NodeId
                && demand.Node.Fence = node.Fence
            | false, _ -> false)

    let enqueueEvidence sessionId physicalUserMessageId =
        match bySession.TryGetValue sessionId with
        | true, current when current.PhysicalUserMessageId = physicalUserMessageId ->
            EnqueueEvidence.SameLogicalOperation current.Node
        | true, current -> EnqueueEvidence.NewerPhysicalGeneration current
        | false, _ -> EnqueueEvidence.NewLogicalOperation

    let queueBoundDecision pendingCount =
        if pendingCount < ModelCapacityQueue.MaximumPendingDemands then
            QueueBoundDecision.HasRoom
        else
            QueueBoundDecision.Full

    let enqueueFresh sessionId physicalUserMessageId effectiveAgent previousTarget =
        match queueBoundDecision demands.Count with
        | QueueBoundDecision.Full -> ExecutionAdmissionAcquisition.QueueFull
        | QueueBoundDecision.HasRoom ->
            nextAdmissionDemandSequence <- nextAdmissionDemandSequence + 1L
            nextNodeId <- QueueNodeId.next nextNodeId
            nextFence <- QueueFence.next nextFence

            let completion =
                TaskCompletionSource<ExecutionAdmissionAcquisition>(TaskCreationOptions.RunContinuationsAsynchronously)

            let node =
                ExecutionAdmissionQueueNode.Create(
                    owner,
                    nextNodeId,
                    nextFence,
                    sessionId,
                    physicalUserMessageId,
                    completion
                )

            let demand =
                { Sequence = nextAdmissionDemandSequence
                  SessionId = sessionId
                  PhysicalUserMessageId = physicalUserMessageId
                  EffectiveAgent = effectiveAgent
                  PreviousTarget = previousTarget
                  Node = node }

            demands.Add demand
            bySession.[sessionId] <- demand
            ExecutionAdmissionAcquisition.Queued node

    member _.Enqueue
        (
            sessionId: string,
            physicalUserMessageId: string,
            effectiveAgent: string,
            previousTarget: ModelRoutingTarget option
        ) : ExecutionAdmissionAcquisition =
        match enqueueEvidence sessionId physicalUserMessageId with
        | EnqueueEvidence.SameLogicalOperation node -> ExecutionAdmissionAcquisition.Queued node
        | EnqueueEvidence.NewLogicalOperation ->
            enqueueFresh sessionId physicalUserMessageId effectiveAgent previousTarget
        | EnqueueEvidence.NewerPhysicalGeneration existing ->
            complete ExecutionAdmissionAcquisition.Superseded existing |> ignore
            enqueueFresh sessionId physicalUserMessageId effectiveAgent previousTarget

    member _.Snapshot() =
        demands |> Seq.sortBy _.Sequence |> Seq.toArray

    member _.TryCurrent(sessionId: string) =
        match bySession.TryGetValue sessionId with
        | true, demand -> Some demand
        | false, _ -> None

    member _.ContainsSession(sessionId: string) = bySession.ContainsKey sessionId

    member _.TryTake(node: ExecutionAdmissionQueueNode) =
        if knownNode node then
            let demand = bySession.[node.SessionId]
            remove demand |> ignore
            Some demand
        else
            None

    member this.Admit(node: ExecutionAdmissionQueueNode, lease: ExecutionAdmissionLease) =
        match this.TryTake node with
        | Some demand ->
            AsyncSupport.trySetResult demand.Node.Completion (ExecutionAdmissionAcquisition.Admitted lease)
            |> ignore

            counters.Record CapacityTransitionOutcome.Applied
        | None -> counters.Record CapacityTransitionOutcome.StaleFence

    member _.CancelSession(sessionId: string) =
        match bySession.TryGetValue sessionId with
        | true, demand when complete ExecutionAdmissionAcquisition.Cancelled demand ->
            counters.Record CapacityTransitionOutcome.Applied
        | true, _ -> counters.Record CapacityTransitionOutcome.Conflict
        | false, _ -> counters.Record CapacityTransitionOutcome.AlreadyApplied

    member _.CancelExecution(sessionId: string, physicalUserMessageId: string) =
        match bySession.TryGetValue sessionId with
        | true, demand when demand.PhysicalUserMessageId <> physicalUserMessageId ->
            counters.Record CapacityTransitionOutcome.StaleFence
        | true, demand when complete ExecutionAdmissionAcquisition.Cancelled demand ->
            counters.Record CapacityTransitionOutcome.Applied
        | true, _ -> counters.Record CapacityTransitionOutcome.Conflict
        | false, _ -> counters.Record CapacityTransitionOutcome.AlreadyApplied

    member _.SupersedeSession(sessionId: string) =
        match bySession.TryGetValue sessionId with
        | true, demand when complete ExecutionAdmissionAcquisition.Superseded demand ->
            counters.Record CapacityTransitionOutcome.Applied
        | true, _ -> counters.Record CapacityTransitionOutcome.Conflict
        | false, _ -> counters.Record CapacityTransitionOutcome.AlreadyApplied

    member _.Fail(error: exn) =
        let pending = demands.ToArray()
        demands.Clear()
        bySession.Clear()

        pending
        |> Array.iter (fun demand -> trySetException demand.Node.Completion error |> ignore)

    member _.Count = demands.Count
