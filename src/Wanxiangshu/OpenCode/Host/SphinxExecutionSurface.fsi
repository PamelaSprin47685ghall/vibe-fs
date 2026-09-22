namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

module SphinxExecutionSurface =
    val createWithOwner: store: EventStoreHandle -> invoke: (obj -> Task<string>) -> cancel: (string -> Task<unit>) -> ownerOf: (string -> string) -> obj
    val create: store: EventStoreHandle -> invoke: (obj -> Task<string>) -> cancel: (string -> Task<unit>) -> obj
    val run:
        execution: obj -> sessionId: string -> invocationId: string -> question: string ->
        expectTurns: int option -> attachAbort: obj -> Task<obj>
    val tool: execution: obj -> toolModule: obj -> obj
    val dispose: execution: obj -> Task
