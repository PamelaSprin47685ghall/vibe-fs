namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

module CasebookBookkeeperRefreshSurface =

    let private storeOf (value: obj) : IEventStore = (unbox<EventStoreHandle> value).Store

    let refreshStale (store: obj) (root: string) (sessionId: string) : Task<obj> =
        task {
            match! CasebookBookkeeper.refreshStale (storeOf store) root sessionId with
            | Ok value -> return box {| ok = true; value = value |}
            | Error message -> return box {| ok = false; error = message |}
        }
