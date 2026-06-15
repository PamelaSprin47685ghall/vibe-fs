module VibeFs.Shell.OllamaClient

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.IpAllowlist
open VibeFs.Shell.SecureFetch

[<Global("process")>]
let private nodeProcess : obj = jsNative

[<Global>]
type URL(url: string) =
    member _.protocol : string = jsNative
    member _.hostname : string = jsNative

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

let private normalizeOllamaPath (pathname: string) : string =
    if pathname.StartsWith("/") then pathname else $"/{pathname}"

/// Validate a fetch URL: must be http(s) and on an allowed (non-private) host.
let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> o

let validateFetchUrl (url: string) : JS.Promise<string option> =
    async {
        try
            let parsed = URL(url)
            let protocol = parsed.protocol
            if protocol <> "http:" && protocol <> "https:" then
                return Some($"unsupported URL scheme: {protocol}")
            else
                let hostname = parsed.hostname
                return if validateHostname hostname then None else Some "host not allowed"
        with _ -> return Some "invalid URL"
    }
    |> Async.StartAsPromise

/// POST JSON to the Ollama API with the bearer key, returning parsed JSON.
let ollamaPost (pathname: string) (body: obj) (abortSignal: obj option) : JS.Promise<obj> =
    async {
        let url = $"{ollamaApiBase}{normalizeOllamaPath pathname}"
        let bodyStr = JS.JSON.stringify(body)
        let init =
            match abortSignal with
            | Some signal -> postInitWithSignal (getOllamaApiKey ()) bodyStr signal
            | None -> postInitNoSignal (getOllamaApiKey ()) bodyStr
        let! response = secureFetch url init |> Async.AwaitPromise
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
            return raise (exn $"Ollama API error ({status}): {detail}")
        else
            let! json = response?json() |> asPromise<obj> |> Async.AwaitPromise
            return json
    }
    |> Async.StartAsPromise
