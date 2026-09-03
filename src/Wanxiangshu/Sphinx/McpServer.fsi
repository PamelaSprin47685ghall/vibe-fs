namespace Wanxiangshu.Sphinx

open Fable.Core
open Wanxiangshu.Persistence.EventStore

module McpServer =
    val create: store: SessionStore -> obj
    val createDurable: sessions: SessionStore -> events: IEventStore -> obj
    val serveStdio: store: SessionStore -> JS.Promise<unit>
    val serveDurable: sessions: SessionStore -> events: IEventStore -> JS.Promise<unit>
    val serveDefault: unit -> JS.Promise<unit>
