namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks

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

module internal ModelCapacityQueue =
    [<Literal>]
    val ContractVersion: int = 1

    [<Literal>]
    val MaximumPendingDemands: int = 32

type internal CapacityStepDemandQueue<'target> =
    new: unit -> CapacityStepDemandQueue<'target>
    member NextSequence: unit -> int64
    member TryAdd: demand: CapacityStepDemand<'target> -> bool
    member Remove: demand: CapacityStepDemand<'target> -> bool
    member Clear: unit -> unit
    member Snapshot: unit -> CapacityStepDemand<'target> array
    member Count: int

type internal ExecutionAdmissionQueue =
    new: owner: obj * counters: CapacityTransitionCounters -> ExecutionAdmissionQueue

    member Enqueue:
        sessionId: string *
        physicalUserMessageId: string *
        effectiveAgent: string *
        previousTarget: ModelRoutingTarget option ->
            ExecutionAdmissionAcquisition

    member Snapshot: unit -> ExecutionAdmissionDemand array
    member TryCurrent: sessionId: string -> ExecutionAdmissionDemand option
    member ContainsSession: sessionId: string -> bool
    member TryTake: node: ExecutionAdmissionQueueNode -> ExecutionAdmissionDemand option
    member Admit: node: ExecutionAdmissionQueueNode * lease: ExecutionAdmissionLease -> CapacityTransitionOutcome
    member CancelSession: sessionId: string -> CapacityTransitionOutcome
    member CancelExecution: sessionId: string * physicalUserMessageId: string -> CapacityTransitionOutcome
    member SupersedeSession: sessionId: string -> CapacityTransitionOutcome
    member Fail: error: exn -> unit
    member Count: int
