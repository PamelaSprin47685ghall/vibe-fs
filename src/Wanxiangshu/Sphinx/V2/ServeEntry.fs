namespace Wanxiangshu.Sphinx.V2

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Composition
open Wanxiangshu.Sphinx.V2.Hosts

/// The Sphinx v2 serve entry.
///
/// WHAT[sphinx-v2-033]: production requires a durable directory. There is no in-memory
/// fallback: an inquiry whose facts cannot survive a restart is not an inquiry, and
/// silently starting one would let a caller believe they had durability.
module ServeEntry =

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Import("resolve", "node:path")>]
    let private resolve (path: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let private moduleUrl () : string = jsNative

    [<Emit("process.argv[1] || ''")>]
    let private entryArgument () : string = jsNative

    [<Emit("console.error($0)")>]
    let private consoleError (line: string) : unit = jsNative

    [<Emit("process.exit(1)")>]
    let private exitFailure () : unit = jsNative

    [<Emit("$0.catch($1)")>]
    let private catchPromise (promise: JS.Promise<unit>) (onError: obj -> unit) : unit = jsNative

    [<Emit("Promise.resolve()")>]
    let private resolved: JS.Promise<unit> = jsNative

    /// SPHINX_COMMON_DIR selects the durable workspace. Missing or blank is a startup
    /// failure in production, never a silent fallback to a memory store.
    let private commonDirEnv = "SPHINX_COMMON_DIR"

    let private readCommonDir () : string option =
        match Environment.GetEnvironmentVariable commonDirEnv with
        | null -> None
        | value when String.IsNullOrWhiteSpace value -> None
        | value -> Some(value)

    /// The durable store this entry serves. It carries the v2 rule program, so the
    /// inquiry state a caller reads is the state the canonical spine folded.
    let createStore (commonDir: string) : Result<IEventStore, string> =
        Bind.createDurableStore commonDir (Guid.NewGuid().ToString("N"))

    let serveDefault () : JS.Promise<unit> =
        match readCommonDir () with
        | None ->
            consoleError "[sphinx-mcp] SPHINX_COMMON_DIR is required: production Sphinx needs a durable workspace"

            exitFailure ()
            resolved
        | Some dir -> Mcp.boot dir

    let private runIfEntryPoint () =
        let argument = entryArgument ()

        if argument <> "" && resolve argument = (moduleUrl () |> fileURLToPath) then
            catchPromise (serveDefault ()) (fun error ->
                consoleError (sprintf "[sphinx-mcp] fatal: %s" (string error))
                exitFailure ())

    runIfEntryPoint ()
