namespace Wanxiangshu.OpenCode

open Wanxiangshu.Persistence.EventStore

module WorkspaceEventStoreSurface =
    let acquire (commonDir: string) : obj =
        WorkspaceEventStore.acquire commonDir |> box

    let allHeadsCount (store: obj) : int =
        (unbox<IEventStore> store).AllHeads() |> List.length

    let release (commonDir: string) : unit = WorkspaceEventStore.release commonDir
