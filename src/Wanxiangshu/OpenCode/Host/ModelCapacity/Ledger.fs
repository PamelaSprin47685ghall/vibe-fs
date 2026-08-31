namespace Wanxiangshu.OpenCode

open System.Collections.Generic


/// One opaque credit = one physical occurrence in scheduler `running`.
type internal CapacityLedger<'target>() =
    let gate = obj ()
    let entries = Dictionary<CapacityCreditId, 'target>()
    // DSL-MUTABLE: resource — monotonic opaque capacity-credit identity
    let mutable nextCredit = CapacityCreditId.initial

    member _.Acquire(target: 'target) =
        lock gate (fun () ->
            nextCredit <- CapacityCreditId.next nextCredit
            entries.[nextCredit] <- target
            nextCredit)

    member _.Retarget(credit: CapacityCreditId, target: 'target) =
        lock gate (fun () ->
            if entries.ContainsKey credit then
                entries.[credit] <- target
                CapacityTransitionOutcome.Applied
            else
                CapacityTransitionOutcome.StaleFence)

    member _.Release(credit: CapacityCreditId) =
        lock gate (fun () ->
            if entries.Remove credit then
                CapacityTransitionOutcome.Applied
            else
                CapacityTransitionOutcome.AlreadyApplied)

    member _.Entries() =
        lock gate (fun () ->
            entries
            |> Seq.map (fun (KeyValue(credit, target)) -> credit, target)
            |> Seq.toArray)

    member this.Snapshot() = this.Entries() |> Array.map snd
