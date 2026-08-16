namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Host.Contract
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.EventStore

/// JS-native provider fetch boundary. The Host schema, Casebook index, replay,
/// and Bookkeeper remain inside the owner; callers pass only plain Host values
/// and the opaque EventStore capability.
module CasebookFetchSurface =

    let private storeOf (value: obj) : IEventStore =
        (unbox<EventStoreHandle> value).Store

    let contract (toolModule: obj) (workspaceRoot: string) (store: obj) : obj =
        let spec = FetchTool.spec (ToolHostCodec.factory toolModule) workspaceRoot (storeOf store)

        box
            {| name = spec.Name
               description = spec.Description
               argumentNames = spec.Arguments |> List.map fst |> List.toArray
               execute =
                   fun args context ->
                       task {
                           let! result = spec.Execute (HostToolArguments args) (ToolHostCodec.decodeContext context)
                           return ToolResultBound.bound result
                       } |}
