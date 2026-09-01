namespace Wanxiangshu.OpenCode

type internal CapacityLedger<'target> =
    new: unit -> CapacityLedger<'target>
    member Acquire: target: 'target -> CapacityCreditId
    member Retarget: credit: CapacityCreditId * target: 'target -> CapacityTransitionOutcome
    member Release: credit: CapacityCreditId -> CapacityTransitionOutcome
    member Entries: unit -> (CapacityCreditId * 'target) array
    member Snapshot: unit -> 'target array
