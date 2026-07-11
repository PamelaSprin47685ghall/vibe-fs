module Wanxiangshu.Shell.FallbackRuntimeStateGates

open Wanxiangshu.Kernel.FallbackRuntimeFlags

let emptyConsumed = Map.empty<string, FallbackConsumedStatus>
let emptyActiveGates = Set.empty<FallbackSessionGateFlag>

let getConsumedBool (consumed: Map<string, FallbackConsumedStatus>) (sessionID: string) : bool option =
    consumedToBoolOption (Map.tryFind sessionID consumed)

let setConsumedBool
    (consumed: Map<string, FallbackConsumedStatus>)
    (sessionID: string)
    (value: bool)
    : Map<string, FallbackConsumedStatus> =
    Map.add sessionID (consumedFromBool value) consumed

let clearConsumedMap
    (consumed: Map<string, FallbackConsumedStatus>)
    (sessionID: string)
    : Map<string, FallbackConsumedStatus> =
    Map.remove sessionID consumed

let private flagsFor
    (store: Map<string, Set<FallbackSessionGateFlag>>)
    (sessionID: string)
    : Set<FallbackSessionGateFlag> =
    Map.tryFind sessionID store |> Option.defaultValue emptyActiveGates

let isGateActive
    (store: Map<string, Set<FallbackSessionGateFlag>>)
    (sessionID: string)
    (flag: FallbackSessionGateFlag)
    : bool =
    Set.contains flag (flagsFor store sessionID)

let setGateActive
    (store: Map<string, Set<FallbackSessionGateFlag>>)
    (sessionID: string)
    (flag: FallbackSessionGateFlag)
    (value: bool)
    : Map<string, Set<FallbackSessionGateFlag>> =
    let current = flagsFor store sessionID

    let next =
        if value then
            Set.add flag current
        else
            Set.remove flag current

    if Set.isEmpty next then
        Map.remove sessionID store
    else
        Map.add sessionID next store

let removeSessionGates
    (store: Map<string, Set<FallbackSessionGateFlag>>)
    (sessionID: string)
    : Map<string, Set<FallbackSessionGateFlag>> =
    Map.remove sessionID store
