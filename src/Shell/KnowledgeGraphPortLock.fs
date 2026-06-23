module VibeFs.Shell.KnowledgeGraphPortLock

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

let rec private acquireLoopUntil (port: int) (deadlineMs: int64) (retryDelayMs: int) : JS.Promise<Result<obj, string>> =
    promise {
        try
            let! server = listenServer port
            return Ok server
        with _ ->
            if System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= deadlineMs then
                return Error $"Timed out acquiring knowledge graph port lock on 127.0.0.1:{port}"
            else
                do! Promise.sleep retryDelayMs
                return! acquireLoopUntil port deadlineMs retryDelayMs
    }

let withKnowledgeGraphPortLock (timeoutMs: int64) (retryDelayMs: int) (workspaceRoot: string) (work: unit -> JS.Promise<'a>) : JS.Promise<Result<'a, string>> =
    promise {
        let port = lockPortForPath workspaceRoot
        let deadlineMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timeoutMs
        let! acquired = acquireLoopUntil port deadlineMs retryDelayMs
        match acquired with
        | Error e -> return Error e
        | Ok server ->
            try
                let! result = work ()
                do! closeServer server
                return Ok result
            with ex ->
                do! closeServer server
                return Error (string ex)
    }
