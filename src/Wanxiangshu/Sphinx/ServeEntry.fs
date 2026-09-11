namespace Wanxiangshu.Sphinx

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Persistence.EventStore

module ServeEntry =

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Import("resolve", "node:path")>]
    let private resolve (path: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let private moduleUrl () : string = jsNative

    [<Emit("process.argv[1] || ''")>]
    let private entryArgument () : string = jsNative

    [<Emit("$0.catch($1)")>]
    let private catchPromise (promise: JS.Promise<unit>) (onError: obj -> unit) : unit = jsNative

    [<Emit("console.error($0)")>]
    let private consoleError (line: string) : unit = jsNative

    [<Emit("process.exit(1)")>]
    let private exitFailure () : unit = jsNative

    // WHAT[EPI-030]: SPHINX_COMMON_DIR selects the durable workspace. Missing
    // or blank keeps the legacy in-memory server with no store contact.
    let private sphinxCommonDirEnv = "SPHINX_COMMON_DIR"

    let private readCommonDir () : string option =
        match Environment.GetEnvironmentVariable sphinxCommonDirEnv with
        | null
        | "" -> None
        | value when String.IsNullOrWhiteSpace value -> None
        | value -> Some value

    let private serveBooted (events: IEventStore) : JS.Promise<unit> =
        match McpServer.bootDurable events with
        | Ok sessions -> McpServer.serveDurable sessions events
        | Error message -> failwith message

    let private serveDurableDir (commonDir: string) =
        try
            // Sphinx serve replays its own durable sessions: the journal-only
            // spine plus both Sphinx-owned oracles, in registration order.
            let integrator =
                CanonicalIntegrator.createWithRules (CanonicalIntegrator.baseRules @ SphinxIntegrationRules.rules)

            let events =
                EventStore.createLocal commonDir (Guid.NewGuid().ToString("N")) integrator

            serveBooted events
        with ex ->
            consoleError (sprintf "[sphinx-mcp] durable boot failed: %s" ex.Message)
            exitFailure ()
            reraise ()

    let serveDefault () =
        match readCommonDir () with
        | None -> McpServer.serveStdio Session.defaultStore
        | Some dir -> serveDurableDir dir

    let private runIfEntryPoint () =
        let argument = entryArgument ()

        if argument <> "" && resolve argument = (moduleUrl () |> fileURLToPath) then
            catchPromise (serveDefault ()) (fun error ->
                consoleError (sprintf "[sphinx-mcp] fatal: %s" (string error))
                exitFailure ())

    runIfEntryPoint ()
