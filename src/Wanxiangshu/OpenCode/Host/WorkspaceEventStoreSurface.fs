namespace Wanxiangshu.OpenCode

open Wanxiangshu.Persistence.EventStore

module WorkspaceEventStoreSurface =
    let acquire (commonDir: string) : obj =
        WorkspaceEventStore.acquire commonDir |> box

    let activate (store: obj) : unit =
        (unbox<IEventStore> store).TryCurrent "Journal" |> ignore

    let release (commonDir: string) : unit = WorkspaceEventStore.release commonDir
