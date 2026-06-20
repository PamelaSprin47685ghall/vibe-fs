module VibeFs.Shell.WikiPortLock

open Fable.Core
open Fable.Core.JsInterop

[<Import("createServer", "node:net")>]
let private createServer () : obj = jsNative

let lockPortForPath (workspaceRoot: string) : int =
    let hashed =
        workspaceRoot
        |> Seq.fold (fun acc ch -> acc * 31 + int ch) 17
        &&& System.Int32.MaxValue
    49152 + (hashed % 16384)

let private listenServer (port: int) : JS.Promise<obj> =
    Promise.create (fun resolve reject ->
        let server = createServer ()
        let listeningHandler = System.Func<unit>(fun () -> resolve server)
        let errorHandler = System.Func<obj, unit>(fun error ->
            try server?close() |> ignore with _ -> ()
            reject (exn (string error)))
        server?once("listening", listeningHandler) |> ignore
        server?once("error", errorHandler) |> ignore
        server?listen(port, "127.0.0.1") |> ignore)

let private closeServer (server: obj) : JS.Promise<unit> =
    Promise.create (fun resolve _reject ->
        try
            let closeHandler = System.Func<unit>(fun () -> resolve ())
            server?close(closeHandler) |> ignore
        with _ -> resolve ())

let rec private acquireLoop (port: int) : JS.Promise<obj> =
    promise {
        try
            let! server = listenServer port
            return server
        with _ ->
            do! Promise.sleep 1000
            return! acquireLoop port
    }

let withWikiPortLock (workspaceRoot: string) (work: unit -> JS.Promise<'a>) : JS.Promise<'a> =
    promise {
        let port = lockPortForPath workspaceRoot
        let! server = acquireLoop port
        try
            let! result = work ()
            do! closeServer server
            return result
        with ex ->
            do! closeServer server
            return! Promise.reject ex
    }
