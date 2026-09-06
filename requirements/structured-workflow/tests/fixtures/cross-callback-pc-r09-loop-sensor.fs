module CrossCallbackPcR09LoopSensor

open System.Collections.Generic

type DegenerationKind =
    | BoundedLoop
    | StalledOutput

// R09 boundary shape: session-only degeneration anomaly latch
// ResetDetector keeps armed; ConsumeAbortCause clears presence and triggers continuation
let mutable armedAnomalies = Map.empty<string, DegenerationKind>

type Sensor() =
    member _.ArmAnomaly(sessionId: string, kind: DegenerationKind) =
        armedAnomalies <- Map.add sessionId kind armedAnomalies

    member _.ConsumeAbortCause(sessionId: string) : DegenerationKind option =
        match Map.tryFind sessionId armedAnomalies with
        | Some cause ->
            armedAnomalies <- Map.remove sessionId armedAnomalies
            Some cause
        | None -> None
