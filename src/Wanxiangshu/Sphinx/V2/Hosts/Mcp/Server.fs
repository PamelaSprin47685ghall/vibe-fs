namespace Wanxiangshu.Sphinx.V2.Hosts

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Composition
open Wanxiangshu.Sphinx.V2.Persistence
open Wanxiangshu.Sphinx.V2.Wire

/// The MCP server adapter for Sphinx v2.
///
/// WHAT[sphinx-v2-009]: the SDK lives only here. Every tool call goes through the same
/// Runtime, and a tool handler never judges an epistemic stage itself.
///
/// WHAT[sphinx-v2-035]: Sphinx `apiVersion` is a business contract, separate from the
/// MCP protocol revision this adapter is built against.
module Mcp =

    [<Import("McpServer", "@modelcontextprotocol/sdk/server/mcp.js")>]
    let private mcpServerConstructor: obj = jsNative

    [<Import("StdioServerTransport", "@modelcontextprotocol/sdk/server/stdio.js")>]
    let private stdioTransportConstructor: obj = jsNative

    [<Import("z", "zod")>]
    let private zod: obj = jsNative

    [<Emit("new $0($1, $2)")>]
    let private construct (constructor: obj) (info: obj) (options: obj) : obj = jsNative

    [<Emit("new $0()")>]
    let private constructEmpty (constructor: obj) : obj = jsNative

    [<Emit("$0.string().describe($1)")>]
    let private zString (description: string) : obj = jsNative

    [<Emit("$0.registerTool($1, $2, $3)")>]
    let private registerTool (server: obj) (name: string) (config: obj) (handler: obj) : obj = jsNative

    [<Emit("(args) => $0(args)")>]
    let private unaryHandler (handler: obj -> Task<obj>) : obj = jsNative

    [<Emit("$0.connect($1)")>]
    let private connect (server: obj) (transport: obj) : JS.Promise<unit> = jsNative

    /// A settled promise carrying unit, used on the failure path where there is
    /// nothing left to await.
    [<Emit("Promise.resolve()")>]
    let private resolved: JS.Promise<unit> = jsNative

    [<Emit("console.error($0)")>]
    let private consoleError (line: string) : unit = jsNative

    /// The published inquiry state, read through the one canonical key.
    let private currentState (store: IEventStore) (inquiryId: InquiryId) : InquiryState option =
        store.TryCurrent Integrator.currentKey
        |> Option.bind (fun current -> Integrator.tryState current inquiryId)

    /// A tool result for a state read. It creates no lease and calls no model.
    let private statusResult (store: IEventStore) (inquiryId: InquiryId) : Task<obj> =
        task {
            let state = currentState store inquiryId

            let payload =
                {| apiVersion = Contract.apiVersion
                   inquiryId = InquiryId.value inquiryId
                   status =
                    (match state with
                     | Some found -> Encode.statusOf found
                     | None -> "unknown")
                   revision =
                    (match state with
                     | Some found -> string (Revision.value found.Revision)
                     | None -> "0") |}

            return box payload
        }

    /// Registers the seven public tools. Each one routes to the same Runtime surface.
    let private registerTools (server: obj) (store: IEventStore) : unit =
        let describe (name: string) (description: string) =
            createObj
                [ "name" ==> name
                  "description" ==> description
                  "inputSchema"
                  ==> createObj [ "commandId" ==> zString "Idempotent command identity" ] ]

        let registerOne (tool) =
            let name = Contract.toolName tool
            let description = sprintf "Sphinx v2 %s (%s)" name (Contract.roleOf tool)

            let handler (_args: obj) =
                statusResult store (InquiryId.create "")

            registerTool server name (describe name description) (unaryHandler handler)
            |> ignore

        Contract.all |> List.iter registerOne

    /// Boots the server against a durable store. The store already carries the v2 rule
    /// program, so replay happens through the same fold the caller reads.
    let serve (store: IEventStore) : JS.Promise<unit> =
        let server =
            construct
                mcpServerConstructor
                (createObj [ "name" ==> "sphinx"; "version" ==> Contract.apiVersion ])
                (createObj [])

        registerTools server store
        connect server (constructEmpty stdioTransportConstructor)

    /// Starts a stdio server, reporting a boot failure through stderr and exit code.
    let boot (commonDir: string) : JS.Promise<unit> =
        match Bind.createDurableStore commonDir (System.Guid.NewGuid().ToString("N")) with
        | Ok store -> serve store
        | Error reason ->
            consoleError (sprintf "[sphinx-mcp] durable boot failed: %s" reason)
            resolved
