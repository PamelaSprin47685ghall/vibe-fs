namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic

type internal ExecutionCapacityOwner(counters: CapacityTransitionCounters) =
    let gate = obj ()
    let lifecycleBySession = Dictionary<string, ExecutionCapacityLifecycle>()
    // DSL-MUTABLE: resource
    let mutable nextLeaseId = CapacityLeaseId.initial
    let mutable nextFence = CapacityFence.initial

    let knownLifecycle (lease: ExecutionAdmissionLease) =
        match obj.ReferenceEquals(lease.Owner, gate), lifecycleBySession.TryGetValue lease.Identity.SessionId with
        | false, _ -> Error ExecutionAdmissionRejection.WrongFence
        | true, (false, _) -> Error ExecutionAdmissionRejection.StaleLease
        | true, (true, lifecycle) when not (obj.ReferenceEquals(ExecutionCapacityLifecycle.leaseOf lifecycle, lease)) ->
            Error ExecutionAdmissionRejection.StaleLease
        | true, (true, lifecycle) when (ExecutionCapacityLifecycle.leaseOf lifecycle).Fence <> lease.Fence ->
            Error ExecutionAdmissionRejection.WrongFence
        | true, (true, lifecycle) -> Ok lifecycle

    let knownLease (lease: ExecutionAdmissionLease) =
        match isNull (box lease) with
        | true -> Error ExecutionAdmissionRejection.UnknownLease
        | false -> knownLifecycle lease

    let apply lifecycle evidence =
        match ExecutionCapacityLifecycle.decide (Some lifecycle) evidence with
        | ExecutionCapacityDecision.Transitioned next ->
            let lease = ExecutionCapacityLifecycle.leaseOf next
            lifecycleBySession.[lease.Identity.SessionId] <- next
            ExecutionCapacityDecision.Transitioned next
        | decision -> decision

    let applyKnown lease evidence =
        knownLease lease
        |> Result.bind (fun lifecycle -> Ok(apply lifecycle evidence))
        |> Result.defaultWith ExecutionCapacityDecision.Rejected

    let outcomeOf =
        function
        | ExecutionCapacityDecision.Transitioned _ -> CapacityTransitionOutcome.Applied
        | ExecutionCapacityDecision.Idempotent -> CapacityTransitionOutcome.AlreadyApplied
        | ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.UnknownLease
        | ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.WrongFence
        | ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.StaleLease ->
            CapacityTransitionOutcome.StaleFence
        | ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.WrongSession
        | ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.WrongPhysicalUserMessage
        | ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.WrongEffectiveAgent
        | ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.WrongTarget
        | ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.IllegalTransition
        | ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.OppositeTerminalConflict ->
            CapacityTransitionOutcome.Conflict

    let record decision =
        decision |> outcomeOf |> counters.Record

    let finishRelease lease release =
        function
        | ExecutionCapacityDecision.Transitioned releasing ->
            apply releasing (ExecutionCapacityEvidence.CompleteRelease(lease, release))
        | decision -> decision

    let beginRelease lifecycle lease release evidence =
        apply lifecycle evidence |> finishRelease lease release

    let sameIssue
        (identity: ExecutionAdmissionExactIdentity)
        (capacityCredit: CapacityCreditId)
        (lifecycle: ExecutionCapacityLifecycle)
        : bool =
        let current = ExecutionCapacityLifecycle.leaseOf lifecycle
        current.Identity = identity && current.CapacityCredit = capacityCredit

    let issueFresh
        (identity: ExecutionAdmissionExactIdentity)
        (capacityCredit: CapacityCreditId)
        : ExecutionAdmissionLease =
        nextLeaseId <- CapacityLeaseId.next nextLeaseId
        nextFence <- CapacityFence.next nextFence

        let lease =
            ExecutionAdmissionLease.Create(gate, capacityCredit, nextLeaseId, nextFence, identity)

        match ExecutionCapacityLifecycle.decide None (ExecutionCapacityEvidence.Acquire lease) with
        | ExecutionCapacityDecision.Transitioned pending ->
            lifecycleBySession.[identity.SessionId] <- pending
            lease
        | _ -> invalidOp "execution-model-routing: free capacity owner rejected acquire"

    let physicalDecision
        (physicalUserMessageId: string)
        (lifecycle: ExecutionCapacityLifecycle)
        : ExecutionCapacityDecision =
        let lease = ExecutionCapacityLifecycle.leaseOf lifecycle

        match lease.Identity.PhysicalUserMessageId = physicalUserMessageId with
        | false -> ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.WrongPhysicalUserMessage
        | true ->
            ExecutionCapacityEvidence.BeginPhysicalCompletion lease
            |> beginRelease lifecycle lease ExecutionCapacityRelease.PhysicalCompletion

    let issueLocked
        (identity: ExecutionAdmissionExactIdentity)
        (capacityCredit: CapacityCreditId)
        : ExecutionAdmissionLease =
        match lifecycleBySession.TryGetValue identity.SessionId with
        | true, lifecycle when sameIssue identity capacityCredit lifecycle ->
            ExecutionCapacityLifecycle.leaseOf lifecycle
        | true, _
        | false, _ -> issueFresh identity capacityCredit

    let releasePhysicalLocked (sessionId: string) (physicalUserMessageId: string) : ExecutionCapacityDecision =
        match lifecycleBySession.TryGetValue sessionId with
        | true, lifecycle when
            (ExecutionCapacityLifecycle.leaseOf lifecycle).Identity.PhysicalUserMessageId = physicalUserMessageId
            ->
            physicalDecision physicalUserMessageId lifecycle
        | true, _ -> ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.StaleLease
        | false, _ -> ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.UnknownLease

    member _.Issue(identity: ExecutionAdmissionExactIdentity, capacityCredit: CapacityCreditId) =
        lock gate (fun () -> issueLocked identity capacityCredit)

    member _.Target(lease: ExecutionAdmissionLease) =
        lock gate (fun () -> knownLease lease |> Result.map (fun _ -> lease.Identity.Target))

    member _.Commit(lease: ExecutionAdmissionLease, observed: ExecutionAdmissionExactIdentity) =
        lock gate (fun () -> ExecutionCapacityEvidence.Commit(lease, observed) |> applyKnown lease |> record)

    member _.ReleaseBeforeProvider(lease: ExecutionAdmissionLease, observed: ExecutionAdmissionExactIdentity) =
        lock gate (fun () ->
            knownLease lease
            |> Result.bind (fun lifecycle ->
                ExecutionCapacityEvidence.BeginReleaseBeforeProvider(lease, observed)
                |> beginRelease lifecycle lease ExecutionCapacityRelease.BeforeProvider
                |> Ok)
            |> Result.defaultWith ExecutionCapacityDecision.Rejected
            |> record)

    member _.ReleasePhysical(sessionId: string, physicalUserMessageId: string) =
        lock gate (fun () -> releasePhysicalLocked sessionId physicalUserMessageId)
        |> record

    member _.LifecycleName(lease: ExecutionAdmissionLease) =
        lock gate (fun () ->
            knownLease lease
            |> Result.map (function
                | ExecutionCapacityLifecycle.Pending _ -> "Pending"
                | ExecutionCapacityLifecycle.Committed _ -> "Committed"
                | ExecutionCapacityLifecycle.Releasing _ -> "Releasing"
                | ExecutionCapacityLifecycle.Released _ -> "Released"))
