module Wanxiangshu.Mission.Relay.Retirement.Surface

type AdmissionFence =
    private
        { IncumbentId: string
          FrozenAt: int }

type NudgeState = private NudgeState of Set<string>

let decide (resources: obj array) (_: obj) =
    if resources.Length = 0 then
        box {| decision = "Retire" |}
    else
        box
            {| decision = "BlockedByResources"
               blockers = resources |}

let freeze incumbentId eventPosition =
    { IncumbentId = incumbentId
      FrozenAt = eventPosition }

let fenceAppliesTo fence incumbentId = fence.IncumbentId = incumbentId

let admitResource fence eventPosition =
    if eventPosition < fence.FrozenAt then
        box
            {| ok = false
               error = "StaleIncumbencyAdmissionFence" |}
    else
        box
            {| ok = false
               error = "IncumbencyAdmissionsFrozen" |}

let emptyNudges () = NudgeState Set.empty

let private key (incumbentId: string) (causalFrontier: int) =
    incumbentId + "\u001f" + string causalFrontier

let observeNormalTerminal (NudgeState scheduled) incumbentId causalFrontier =
    let current = key incumbentId causalFrontier

    if Set.contains current scheduled then
        box
            {| scheduled = false
               state = NudgeState scheduled |}
    else
        box
            {| scheduled = true
               state = NudgeState(Set.add current scheduled) |}

let private ignored (state: NudgeState) =
    box {| scheduled = false; state = state |}

let observeProviderFailure (state: NudgeState) (_: string) (_: int) = ignored state
let observeAuthorityRevoked (state: NudgeState) (_: string) (_: int) = ignored state
let nudgeCount (NudgeState scheduled) = Set.count scheduled
