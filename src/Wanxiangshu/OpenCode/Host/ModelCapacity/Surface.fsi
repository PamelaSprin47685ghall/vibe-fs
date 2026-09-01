namespace Wanxiangshu.OpenCode

type internal ExecutionCapacityOwner =
    new: counters: CapacityTransitionCounters -> ExecutionCapacityOwner

    member Issue:
        identity: ExecutionAdmissionExactIdentity * capacityCredit: CapacityCreditId -> ExecutionAdmissionLease

    member Target: lease: ExecutionAdmissionLease -> Result<ModelRoutingTarget, ExecutionAdmissionRejection>

    member Commit:
        lease: ExecutionAdmissionLease * observed: ExecutionAdmissionExactIdentity -> CapacityTransitionOutcome

    member ReleaseBeforeProvider:
        lease: ExecutionAdmissionLease * observed: ExecutionAdmissionExactIdentity -> CapacityTransitionOutcome

    member ReleasePhysical: sessionId: string * physicalUserMessageId: string -> CapacityTransitionOutcome
    member LifecycleName: lease: ExecutionAdmissionLease -> Result<string, ExecutionAdmissionRejection>
