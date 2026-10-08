namespace Wanxiangshu.Sphinx.V2.Hosts

open Fable.Core
open Wanxiangshu.Persistence.EventStore

module McpSurface =
    let serveConfigured (store: EventStoreHandle) (configuration: obj) : JS.Promise<unit> =
        Mcp.serveConfigured store.Store (if isNull configuration then None else Some configuration)
