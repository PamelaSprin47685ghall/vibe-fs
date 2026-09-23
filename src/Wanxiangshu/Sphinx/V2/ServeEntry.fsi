namespace Wanxiangshu.Sphinx.V2

open System
open Fable.Core
open Wanxiangshu.Persistence.EventStore

module ServeEntry =
    /// The durable store this entry serves, carrying the v2 rule program.
    val createStore: commonDir: string -> Result<IEventStore, string>
    val serveDefault: unit -> JS.Promise<unit>
