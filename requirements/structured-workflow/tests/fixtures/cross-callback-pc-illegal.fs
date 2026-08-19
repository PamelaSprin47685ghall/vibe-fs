module Sample

type CounterfactualAwait =
    | AwaitFirst of string
    | AwaitSecond of string

// DSL-MUTABLE: resource — counterfactual await registry
let counterfactualAwait = Dictionary<string, CounterfactualAwait>()

type Collector() =
    member _.Arm(sessionId: string) =
        counterfactualAwait.[sessionId] <- AwaitFirst("run-1")

    member _.TryTakePair(sessionId: string) =
        match counterfactualAwait.TryGetValue(sessionId) with
        | true, value -> counterfactualAwait.Remove(sessionId) |> ignore; Some value
        | _ -> None
