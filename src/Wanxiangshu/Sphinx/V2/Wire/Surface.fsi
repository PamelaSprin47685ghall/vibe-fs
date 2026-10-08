namespace Wanxiangshu.Sphinx.V2.Wire

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Composition

module Surface =
    [<Literal>]
    val apiVersion: string = "2"

    val isTool: string -> bool

    /// Opens the canonical durable store. Null configuration permits reads only.
    val create: commonDir: string -> writerId: string -> configuration: obj -> RuntimeHandle

    /// Binds the original Wire composition to the caller's canonical store capability.
    val createWithStore: store: EventStoreHandle -> configuration: obj -> RuntimeHandle

    val dispose: RuntimeHandle -> unit

    /// Public arguments and results are native JSON; the handle is opaque.
    val start: RuntimeHandle -> args: obj -> Task<obj>
    val status: RuntimeHandle -> args: obj -> obj
    val exportInquiry: RuntimeHandle -> args: obj -> obj
    val submitResults: RuntimeHandle -> args: obj -> obj
