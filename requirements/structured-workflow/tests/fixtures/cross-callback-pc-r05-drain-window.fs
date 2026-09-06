module CrossCallbackPcR05DrainWindow

open System.Collections.Generic

type DrainWindow =
    | Closed
    | Open of string

// R05 boundary shape: DrainWindow presence latch wrapped in mutable Map
// SetDrainWindow called on root arrival; IsDrainOpen probes presence to bypass sealed quiescence
let mutable drainWindows = Map.empty<string, DrainWindow>

type Scope() =
    member _.SetDrainWindow(sessionId: string, window: DrainWindow) =
        drainWindows <- Map.add sessionId window drainWindows

    member _.IsDrainOpen(sessionId: string) : bool =
        match Map.tryFind sessionId drainWindows with
        | Some (DrainWindow.Open _) -> true
        | _ -> false
