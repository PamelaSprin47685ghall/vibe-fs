module VibeFs.Shell.OllamaClient

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain

[<Global("process")>]
let private nodeProcess : obj = jsNative

[<Global>]
let private fetch (url: string) (init: obj) : JS.Promise<obj> = jsNative

let postInitNoSignal (apiKey: string) (body: string) : obj =
    createObj [
        "method" ==> "POST"
        "headers" ==> createObj [
            "Content-Type" ==> "application/json"
            "Authorization" ==> $"Bearer {apiKey}"
        ]
        "body" ==> body
    ]

let postInitWithSignal (apiKey: string) (body: string) (signal: obj) : obj =
    createObj [
        "method" ==> "POST"
        "headers" ==> createObj [
            "Content-Type" ==> "application/json"
            "Authorization" ==> $"Bearer {apiKey}"
        ]
        "body" ==> body
        "signal" ==> signal
    ]

let responseMethod0 (response: obj) (methodName: string) : obj =
    response?(methodName)()

let ollamaApiBase = "https://ollama.com/api"

let getOllamaApiKey () : string =
    let env = nodeProcess?env
    if Dyn.isNullish env then ""
    else
        let key = env?("OLLAMA_API_KEY")
        if Dyn.isNullish key then "" else string key

let requireOllamaApiKey (apiKey: string) : Result<string, string> =
    let trimmed = apiKey.Trim()
    if trimmed = "" then Error "Missing OLLAMA_API_KEY environment variable." else Ok trimmed

let private validatedOllamaApiKey () : Result<string, DomainError> =
    requireOllamaApiKey (getOllamaApiKey ())
    |> Result.mapError (fun _ -> UpstreamRefused "Missing OLLAMA_API_KEY environment variable.")

let private normalizeOllamaPath (pathname: string) : string =
    if pathname.StartsWith("/") then pathname else $"/{pathname}"

let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> o

/// POST JSON to the Ollama API with the bearer key, returning parsed JSON.
let ollamaPost (pathname: string) (body: obj) (abortSignal: obj option) : JS.Promise<Result<obj, DomainError>> =
    async {
        match validatedOllamaApiKey () with
        | Error e -> return Error e
        | Ok apiKey ->
            let url = $"{ollamaApiBase}{normalizeOllamaPath pathname}"
            let bodyStr = JS.JSON.stringify(body)
            let init =
                match abortSignal with
                | Some signal -> postInitWithSignal apiKey bodyStr signal
                | None -> postInitNoSignal apiKey bodyStr
            try
                let! response = fetch url init |> Async.AwaitPromise
                let ok = Dyn.truthy (Dyn.get response "ok")
                if not ok then
                    let! text =
                        async {
                            try
                                let! t = response?text() |> asPromise<string> |> Async.AwaitPromise
                                return t
                            with _ -> return ""
                        }
                    let status = Dyn.str response "status"
                    let statusText = Dyn.str response "statusText"
                    let detail = if text <> "" then text else statusText
                    return Error (UpstreamRefused $"Ollama API error ({status}): {detail}")
                else
                    let! json = response?json() |> asPromise<obj> |> Async.AwaitPromise
                    return Ok json
            with ex ->
                return Error (UnknownJsError ex.Message)
    }
    |> Async.StartAsPromise
