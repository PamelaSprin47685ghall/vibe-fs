module VibeFs.Shell.OllamaClient

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.IpAllowlist
open VibeFs.Shell.SecureFetch
open VibeFs.Kernel

[<Emit("process.env.OLLAMA_API_KEY ?? ''")>]
let private envApiKey () : string = jsNative
[<Emit("new URL($0)")>]
let private newUrl (url: string) : obj = jsNative
[<Emit("JSON.stringify($0)")>]
let private stringify (o: obj) : string = jsNative
[<Emit("$0[$1]()")>]
let responseMethod0 (response: obj) (methodName: string) : obj = jsNative

/// Build a fetch init with exact JS field names (method, Content-Type,
/// Authorization).  Anonymous F# records would compile `method'`/`ContentType`
/// verbatim, breaking fetch — so build the literal in JS.  Public so the field
/// shape can be asserted in tests.
[<Emit("({ method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: ('Bearer ' + $0) }, body: $1 })")>]
let postInitNoSignal (apiKey: string) (body: string) : obj = jsNative
[<Emit("({ method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: ('Bearer ' + $0) }, body: $1, signal: $2 })")>]
let postInitWithSignal (apiKey: string) (body: string) (signal: obj) : obj = jsNative

let ollamaApiBase = "https://ollama.com/api"

let getOllamaApiKey () : string = envApiKey ()

let private normalizeOllamaPath (pathname: string) : string =
    if pathname.StartsWith("/") then pathname else $"/{pathname}"

/// Validate a fetch URL: must be http(s) and on an allowed (non-private) host.
let validateFetchUrl (url: string) : JS.Promise<string option> =
    async {
        try
            let parsed = newUrl url
            let protocol = Dyn.str parsed "protocol"
            if protocol <> "http:" && protocol <> "https:" then
                return Some($"unsupported URL scheme: {protocol}")
            else
                let hostname = Dyn.str parsed "hostname"
                return if validateHostname hostname then None else Some "host not allowed"
        with _ -> return Some "invalid URL"
    }
    |> Async.StartAsPromise

/// POST JSON to the Ollama API with the bearer key, returning parsed JSON.
let ollamaPost (pathname: string) (body: obj) (abortSignal: obj option) : JS.Promise<obj> =
    async {
        let url = $"{ollamaApiBase}{normalizeOllamaPath pathname}"
        let bodyStr = stringify body
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
                        let! t = (responseMethod0 response "text" :?> JS.Promise<string>) |> Async.AwaitPromise
                        return t
                    with _ -> return ""
                }
            let status = Dyn.str response "status"
            let statusText = Dyn.str response "statusText"
            let detail = if text <> "" then text else statusText
            return raise (exn $"Ollama API error ({status}): {detail}")
        else
            let! json = (responseMethod0 response "json" :?> JS.Promise<obj>) |> Async.AwaitPromise
            return json
    }
    |> Async.StartAsPromise
