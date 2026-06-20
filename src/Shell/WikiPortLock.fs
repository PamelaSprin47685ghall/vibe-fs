module VibeFs.Shell.WikiPortLock

open Fable.Core
open Fable.Core.JsInterop
open System

[<Import("createServer", "node:net")>]
let private createServer () : obj = jsNative

let lockPortForPath (workspaceRoot: string) : int =
    let hashed =
        workspaceRoot
        |> Seq.fold (fun acc ch -> acc * 31 + int ch) 17
        &&& System.Int32.MaxValue
    49152 + (hashed % 16384)

let private listenServer (port: int) : Async<obj> =
    Async.FromContinuations(fun (resolve, reject, _) ->
        let server = createServer ()
        let listeningHandler = System.Func<unit>(fun () -> resolve server)
        let errorHandler = System.Func<obj, unit>(fun error ->
            try server?close() |> ignore with _ -> ()
            reject (exn (string error)))
        server?once("listening", listeningHandler) |> ignore
        server?once("error", errorHandler) |> ignore
        server?listen(port, "127.0.0.1") |> ignore)

let private closeServer (server: obj) : Async<unit> =
    Async.FromContinuations(fun (resolve, _, _) ->
        try
            let closeHandler = System.Func<unit>(fun () -> resolve ())
            server?close(closeHandler) |> ignore
        with _ -> resolve ())

let rec private acquireLoop (port: int) : Async<obj> =
    async {
        match! Async.Catch (listenServer port) with
        | Choice1Of2 server -> return server
        | Choice2Of2 _ ->
            do! Async.Sleep 1000
            return! acquireLoop port
    }

let withWikiPortLock (workspaceRoot: string) (work: Async<'a>) : Async<'a> =
    async {
        let port = lockPortForPath workspaceRoot
        let! server = acquireLoop port
        let! outcome = Async.Catch work
        do! closeServer server
        match outcome with
        | Choice1Of2 result -> return result
        | Choice2Of2 error -> return raise error
    }
