namespace Wanxiangshu.Sphinx.V2.Hosts

open Fable.Core
open Wanxiangshu.Persistence.EventStore

module McpSurface =
    val serveConfigured: store: EventStoreHandle -> configuration: obj -> JS.Promise<unit>
