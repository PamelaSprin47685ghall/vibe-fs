module CrossCallbackPcR14Counterfactual

open System.Collections.Generic

type AwaitState =
    | AwaitFirst of string
    | AwaitSecond of string

// R14 boundary shape: dual-structure observation await state without unified fold
let mutable armedTargets = Map.empty<string, string>
let mutable observedFirsts = Map.empty<string, AwaitState>

type Collector() =
    member _.Arm(sessionId: string, targetRun: string) =
        armedTargets <- Map.add sessionId targetRun armedTargets

    member _.TryTakePair(sessionId: string) : AwaitState option =
        match Map.tryFind sessionId observedFirsts with
        | Some first ->
            observedFirsts <- Map.remove sessionId observedFirsts
            armedTargets <- Map.remove sessionId armedTargets
            Some first
        | None -> None
