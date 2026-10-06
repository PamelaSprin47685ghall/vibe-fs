namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic

type internal ExecutionCapacityOwner(counters: CapacityTransitionCounters) =
    let gate = obj ()
    let lifecycleBySession = Dictionary<string, ExecutionCapacityLifecycle>()
    // DSL-MUTABLE: resource — committed lifecycles a newer admitted physical
    // replaced on the same session. The superseded generation's in-flight
    // tool-result round still owns provider steps and exact committed-lease
    // reads through its replaced lease until the exact physical releases.
    let supersededCommittedLifecycles =
        Dictionary<string * string, ExecutionCapacityLifecycle>()
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
        | ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.WrongRole
        | ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.WrongParticipant
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

    /// Detached release decision for a superseded generation's remembered
    /// lifecycle: the transition is computed and recorded, but never written
    /// back into `lifecycleBySession` — that slot belongs to the successor
    /// lease the replacement already issued.
    let decideDetached lifecycle evidence =
        match ExecutionCapacityLifecycle.decide (Some lifecycle) evidence with
        | ExecutionCapacityDecision.Transitioned next -> ExecutionCapacityDecision.Transitioned next
        | decision -> decision

    let finishReleaseDetached lease release =
        function
        | ExecutionCapacityDecision.Transitioned releasing ->
            decideDetached releasing (ExecutionCapacityEvidence.CompleteRelease(lease, release))
        | decision -> decision

    let beginReleaseDetached lifecycle lease release evidence =
        decideDetached lifecycle evidence |> finishReleaseDetached lease release

    let supersededPhysicalDecision physicalUserMessageId lifecycle =
        let lease = ExecutionCapacityLifecycle.leaseOf lifecycle

        ExecutionCapacityEvidence.BeginPhysicalCompletion lease
        |> beginReleaseDetached lifecycle lease ExecutionCapacityRelease.PhysicalCompletion

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
            // A committed lifecycle replaced by a newer physical stays readable:
            // the superseded generation's in-flight round still owns provider
            // steps and exact committed-lease reads until its exact terminal
            // releases it. Pending or releasing predecessors hold no in-flight
            // round and are simply replaced.
            (match lifecycleBySession.TryGetValue identity.SessionId with
             | true, ExecutionCapacityLifecycle.Committed oldLease when
                 oldLease.Identity.PhysicalUserMessageId <> identity.PhysicalUserMessageId
                 ->
                 // A session admits a newer physical while an older committed
                 // lease still owns an in-flight round: remember the older
                 // lease so its continuation can finish. Any even-older
                 // superseded entry for this session is now unreachable (its
                 // round was already superseded twice), so drop it to bound
                 // the dictionary to one entry per session.
                 let staleKeys =
                     supersededCommittedLifecycles.Keys
                     |> Seq.filter (fun (sid, _) -> sid = identity.SessionId)
                     |> Seq.toList

                 staleKeys
                 |> List.iter (fun k -> supersededCommittedLifecycles.Remove(k) |> ignore)

                 supersededCommittedLifecycles.[(identity.SessionId, oldLease.Identity.PhysicalUserMessageId)] <-
                     ExecutionCapacityLifecycle.Committed oldLease
             | _ -> ())

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
        | true, _
        | false, _ ->
            // The exact terminal of a superseded in-flight generation settles
            // its remembered committed lifecycle. The dictionary entry is
            // intentionally retained: the superseded generation's in-flight
            // tool-result continuation (continue.1) still needs to read the
            // committed lease via TryReadCommittedLease until its own round
            // truly terminates. The entry is a bounded resource — at most one
            // per session at a time — and the lifecycle transition is computed
            // detached (never written back to lifecycleBySession) so the
            // successor lease's slot is undisturbed.
            let supersededKey = (sessionId, physicalUserMessageId)

            if supersededCommittedLifecycles.ContainsKey supersededKey then
                let lifecycle = supersededCommittedLifecycles.[supersededKey]

                supersededPhysicalDecision physicalUserMessageId lifecycle
            else
                ExecutionCapacityDecision.Rejected ExecutionAdmissionRejection.StaleLease

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

    /// Read-only exact committed lease query; a pending, releasing, released,
    /// absent or wrong-physical lease is not an executable resource. A
    /// superseded in-flight generation's remembered committed lease stays
    /// readable until its exact physical releases.
    member _.TryReadCommittedLease(sessionId: string, physicalUserMessageId: string) =
        lock gate (fun () ->
            match lifecycleBySession.TryGetValue sessionId with
            | true, ExecutionCapacityLifecycle.Committed lease when
                lease.Identity.PhysicalUserMessageId = physicalUserMessageId
                ->
                Some lease
            | _ ->
                let supersededKey = (sessionId, physicalUserMessageId)

                if supersededCommittedLifecycles.ContainsKey supersededKey then
                    match supersededCommittedLifecycles.[supersededKey] with
                    | ExecutionCapacityLifecycle.Committed lease -> Some lease
                    | _ -> None
                else
                    None)
