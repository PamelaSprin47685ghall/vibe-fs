namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

type internal CapacityStepDemand<'target> =
    { Sequence: int64
      SessionId: string
      PhysicalUserMessageId: string
      Target: 'target
      Fence: Set<string>
      RequestKey: string option
      TryOrdinary: 'target array -> bool
      Completion: TaskCompletionSource<unit> }

type internal ExecutionAdmissionDemand =
    { Sequence: int64
      SessionId: string
      PhysicalUserMessageId: string
      Role: Role
      Participant: string
      Purpose: ModelExecutionPurpose
      LenderSessionId: string option
      PreviousTarget: ModelRoutingTarget option
      Node: ExecutionAdmissionQueueNode }

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

/// The slice of the `wanxiangli/admission-queue` execution admission wait
/// queue owner (CUT-10) this host adapter reads. FIFO order, the pending bound,
/// same-logical-operation reuse, newer-physical-generation supersession, exact
/// node take and withdraw are decided there; this adapter keeps the F# node
/// handle and its completion, settles completions and records the counters.
type internal AdmissionQueueLibrary =
    abstract contractVersion: int
    abstract bound: int
    abstract enqueue: sessionId: string * physicalUserMessageId: string * payload: obj -> obj
    abstract current: sessionId: string -> obj
    abstract take: node: obj -> bool
    abstract withdraw: sessionId: string * physicalUserMessageId: obj -> obj
    abstract snapshot: unit -> obj array
    abstract drain: unit -> obj array
    abstract count: unit -> int

module internal AdmissionQueueBinding =
    [<Import("AdmissionQueue", "wanxiangli/admission-queue")>]
    let private queueClass: obj = jsNative

    [<Emit("new $0($1)")>]
    let private construct (cls: obj) (bound: int) : AdmissionQueueLibrary = jsNative

    let create () =
        let queue = construct queueClass ModelCapacityQueue.MaximumPendingDemands

        if queue.contractVersion <> ModelCapacityQueue.ContractVersion then
            invalidOp "execution-model-routing: admission queue contract version diverged"

        queue

    /// The host demand a library node carries; its payload is a holder the
    /// adapter fills right after the library issued the node identity.
    let demand (node: obj) : ExecutionAdmissionDemand = unbox node?payload?demand

    let outcome (value: string) =
        match value with
        | "Applied" -> CapacityTransitionOutcome.Applied
        | "AlreadyApplied" -> CapacityTransitionOutcome.AlreadyApplied
        | "StaleFence" -> CapacityTransitionOutcome.StaleFence
        | other -> invalidOp (sprintf "execution-model-routing: unknown admission queue outcome '%s'" other)

type internal ExecutionAdmissionQueue(owner: obj, counters: CapacityTransitionCounters) =
    let queue = AdmissionQueueBinding.create ()
    // DSL-MUTABLE: resource — host node identity, kept in step with the library node
    let mutable nextNodeId = QueueNodeId.initial
    let mutable nextFence = QueueFence.initial

    let trySetException (completion: TaskCompletionSource<ExecutionAdmissionAcquisition>) (error: exn) =
        try
            completion.SetException error
            true
        with _ ->
            false

    let settle outcome (node: obj) =
        if not (isNull node) then
            AsyncSupport.trySetResult (AdmissionQueueBinding.demand node).Node.Completion outcome
            |> ignore

    /// The host handle must belong to this queue and be the very node the
    /// caller holds: a handle from another queue is not this queue's node.
    let ownedByThisQueue (node: ExecutionAdmissionQueueNode) =
        match isNull (box node) with
        | true -> false
        | false -> obj.ReferenceEquals(node.Owner, owner)

    /// The library node is the session's current demand only when the demand's
    /// own node handle, node id and fence all name the same node the caller
    /// holds. Any difference means the session moved on.
    let isCurrentDemandOf (current: obj) (node: ExecutionAdmissionQueueNode) =
        let demand = AdmissionQueueBinding.demand current

        obj.ReferenceEquals(demand.Node, node)
        && demand.Node.NodeId = node.NodeId
        && demand.Node.Fence = node.Fence

    /// The library node whose host handle is exactly `node`, if it is that session's current demand.
    let libraryNodeOf (node: ExecutionAdmissionQueueNode) =
        let owned = ownedByThisQueue node

        let found = if owned then queue.current node.SessionId else null

        // `&&` short-circuits, so the demand payload is only read for a node
        // the library actually holds.
        let isCurrent = not (isNull found) && isCurrentDemandOf found node

        match isCurrent with
        | true -> Some found
        | false -> None

    let withdraw outcome sessionId (physicalUserMessageId: string option) =
        let physical: obj =
            match physicalUserMessageId with
            | Some value -> box value
            | None -> null

        let result = queue.withdraw (sessionId, physical)
        settle outcome (result?node: obj)
        unbox<string> result?outcome |> AdmissionQueueBinding.outcome |> counters.Record

    member _.Enqueue
        (
            sessionId: string,
            physicalUserMessageId: string,
            role: Role,
            participant: string,
            purpose: ModelExecutionPurpose,
            lenderSessionId: string option,
            previousTarget: ModelRoutingTarget option
        ) : ExecutionAdmissionAcquisition =
        let holder: obj = createObj [ "demand" ==> null ]
        let result = queue.enqueue (sessionId, physicalUserMessageId, holder)
        settle ExecutionAdmissionAcquisition.Superseded (result?superseded: obj)
        let node: obj = result?node

        match unbox<string> result?kind with
        | "Reused" -> ExecutionAdmissionAcquisition.Queued (AdmissionQueueBinding.demand node).Node
        | "Full" -> ExecutionAdmissionAcquisition.QueueFull
        | "Queued" ->
            nextNodeId <- QueueNodeId.next nextNodeId
            nextFence <- QueueFence.next nextFence

            if
                float (QueueNodeId.value nextNodeId) <> unbox<float> node?nodeId
                || float (QueueFence.value nextFence) <> unbox<float> node?fence
            then
                invalidOp "execution-model-routing: host queue node diverged from the admission queue owner"

            let completion =
                TaskCompletionSource<ExecutionAdmissionAcquisition>(TaskCreationOptions.RunContinuationsAsynchronously)

            let hostNode =
                ExecutionAdmissionQueueNode.Create(
                    owner,
                    nextNodeId,
                    nextFence,
                    sessionId,
                    physicalUserMessageId,
                    completion
                )

            let demand =
                { Sequence = int64 (unbox<float> node?sequence)
                  SessionId = sessionId
                  PhysicalUserMessageId = physicalUserMessageId
                  Role = role
                  Participant = participant
                  Purpose = purpose
                  LenderSessionId = lenderSessionId
                  PreviousTarget = previousTarget
                  Node = hostNode }

            holder?demand <- demand
            ExecutionAdmissionAcquisition.Queued hostNode
        | other -> invalidOp (sprintf "execution-model-routing: unknown admission queue kind '%s'" other)

    member _.Snapshot() =
        queue.snapshot () |> Array.map AdmissionQueueBinding.demand

    member _.TryCurrent(sessionId: string) =
        let current = queue.current sessionId

        if isNull current then
            None
        else
            Some(AdmissionQueueBinding.demand current)

    member _.ContainsSession(sessionId: string) = not (isNull (queue.current sessionId))

    member _.TryTake(node: ExecutionAdmissionQueueNode) =
        match libraryNodeOf node with
        | Some current when queue.take current -> Some(AdmissionQueueBinding.demand current)
        | _ -> None

    member this.Admit(node: ExecutionAdmissionQueueNode, lease: ExecutionAdmissionLease) =
        match this.TryTake node with
        | Some demand ->
            AsyncSupport.trySetResult demand.Node.Completion (ExecutionAdmissionAcquisition.Admitted lease)
            |> ignore

            counters.Record CapacityTransitionOutcome.Applied
        | None -> counters.Record CapacityTransitionOutcome.StaleFence

    member _.CancelSession(sessionId: string) =
        withdraw ExecutionAdmissionAcquisition.Cancelled sessionId None

    member _.CancelExecution(sessionId: string, physicalUserMessageId: string) =
        withdraw ExecutionAdmissionAcquisition.Cancelled sessionId (Some physicalUserMessageId)

    member _.SupersedeSession(sessionId: string) =
        withdraw ExecutionAdmissionAcquisition.Superseded sessionId None

    member _.Fail(error: exn) =
        queue.drain ()
        |> Array.iter (fun node -> trySetException (AdmissionQueueBinding.demand node).Node.Completion error |> ignore)

    member _.Count = queue.count ()
