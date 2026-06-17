module VibeFs.Opencode.BacktrackSession

open System.Collections.Generic
open VibeFs.Kernel.BacktrackProjector

type BacktrackSession() =
    let nextIds = Dictionary<string, int>()
    let visibleIdsMap = Dictionary<string, int list>()
    let allocated = Dictionary<string, int>()

    member _.SyncFromMessages(sessionID: string, messages: obj array) =
        let stripped = VibeFs.Kernel.SyntheticIds.stripSyntheticMessages messages
        let ids = visibleIds stripped
        visibleIdsMap.[sessionID] <- ids
        let maxExisting = if ids.IsEmpty then 0 else List.max ids
        let current = match nextIds.TryGetValue sessionID with true, v -> v | false, _ -> 0
        if maxExisting + 1 > current then nextIds.[sessionID] <- maxExisting + 1

    member _.Allocate(sessionID: string, callID: string) : int =
        let current = match nextIds.TryGetValue sessionID with true, v -> v | false, _ -> 0
        nextIds.[sessionID] <- current + 1
        allocated.[callID] <- current
        current

    member _.TryGetAllocated(callID: string) : int option =
        match allocated.TryGetValue callID with true, id -> Some id | false, _ -> None

    member _.GetVisibleIds(sessionID: string) : int list =
        match visibleIdsMap.TryGetValue sessionID with true, ids -> ids | false, _ -> []

    member _.ClearCall(callID: string) =
        allocated.Remove(callID) |> ignore
