module Sample

// DSL-MUTABLE: resource — per-session loop detector registry
let detectors = Dictionary<string, LoopDetector.Detector>()

type Sensor() =
    member _.ResetDetector(sessionId: string) =
        detectors.[sessionId] <- LoopDetector.create()
