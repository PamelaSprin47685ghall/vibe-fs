namespace Wanxiangshu.Sphinx

open Fable.Core
open Fable.Core.JsInterop

module McpServer =

    [<Import("McpServer", "@modelcontextprotocol/sdk/server/mcp.js")>]
    let private mcpServerConstructor: obj = jsNative

    [<Import("StdioServerTransport", "@modelcontextprotocol/sdk/server/stdio.js")>]
    let private stdioTransportConstructor: obj = jsNative

    [<Import("z", "zod")>]
    let private zod: obj = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Import("resolve", "node:path")>]
    let private resolve (path: string) : string = jsNative

    [<Emit("new $0($1)")>]
    let private construct (constructor: obj) (argument: obj) : obj = jsNative

    [<Emit("new $0()")>]
    let private constructEmpty (constructor: obj) : obj = jsNative

    [<Emit("$0.string().describe($1)")>]
    let private zString (z: obj) (description: string) : obj = jsNative

    [<Emit("$0.record($0.string(), $0.any()).describe($1)")>]
    let private zRecord (z: obj) (description: string) : obj = jsNative

    [<Emit("$0.registerTool($1, $2, $3)")>]
    let private registerTool (server: obj) (name: string) (config: obj) (handler: obj) : obj = jsNative

    [<Emit("(args) => $0(args)")>]
    let private unaryHandler (handler: obj -> obj) : obj = jsNative

    [<Emit("$0.connect($1)")>]
    let private connect (server: obj) (transport: obj) : JS.Promise<unit> = jsNative

    [<Emit("JSON.stringify($0)")>]
    let private stringify (value: obj) : string = jsNative

    [<Emit("$0.question")>]
    let private question (args: obj) : string = jsNative

    [<Emit("$0.handle")>]
    let private handle (args: obj) : string = jsNative

    [<Emit("$0.observation")>]
    let private observation (args: obj) : obj = jsNative

    [<Emit("import.meta.url")>]
    let private moduleUrl () : string = jsNative

    [<Emit("process.argv[1] || ''")>]
    let private entryArgument () : string = jsNative

    [<Emit("$0.catch($1)")>]
    let private catchPromise (promise: JS.Promise<unit>) (onError: obj -> unit) : unit = jsNative

    [<Emit("console.error($0)")>]
    let private consoleError (error: obj) : unit = jsNative

    [<Emit("process.exit(1)")>]
    let private exitFailure () : unit = jsNative

    let private content payload =
        createObj
            [ "content"
              ==> [| createObj [ "type" ==> "text"; "text" ==> stringify payload ] |] ]

    let create (store: SessionStore) =
        let server =
            construct mcpServerConstructor (createObj [ "name" ==> "sphinx"; "version" ==> "1.0.0" ])

        registerTool
            server
            "start"
            (createObj
                [ "title" ==> "Start Sphinx inquiry"
                  "description" ==> "Begin a kernel-controlled epistemic inquiry."
                  "inputSchema" ==> createObj [ "question" ==> zString zod "Root question" ] ])
            (unaryHandler (fun args -> store.Start(question args) |> content))
        |> ignore

        registerTool
            server
            "resume"
            (createObj
                [ "title" ==> "Resume Sphinx inquiry"
                  "description"
                  ==> "Continue the same inquiry with the structured observation requested by the kernel."
                  "inputSchema"
                  ==> createObj
                          [ "handle" ==> zString zod "Opaque inquiry handle returned by start"
                            "observation"
                            ==> zRecord zod "Structured observation matching the pending kernel request" ] ])
            (unaryHandler (fun args -> store.Resume(handle args, observation args) |> content))
        |> ignore

        server

    let serveStdio (store: SessionStore) =
        let server = create store
        let transport = constructEmpty stdioTransportConstructor
        connect server transport

    let serveDefault () = serveStdio Session.defaultStore

    let private runIfEntryPoint () =
        let argument = entryArgument ()

        if argument <> "" && resolve argument = (moduleUrl () |> fileURLToPath) then
            catchPromise (serveDefault ()) (fun error ->
                consoleError error
                exitFailure ())

    runIfEntryPoint ()
