module Wanxiangshu.Runtime.WebSearchApi

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.WebFetchGuard
open Thoth.Json

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

[<Global>]
let private fetch (url: string) (init: obj) : JS.Promise<obj> = jsNative

[<Emit("new URL($0, $1).toString()")>]
let private resolveUrl (location: string) (baseUrl: string) : string = jsNative

[<Emit("$0.headers.get('location')")>]
let private getResponseLocation (response: obj) : string = jsNative

let postInit (apiKey: string) (body: string) (signal: obj option) : obj =
    createObj
        [ "method" ==> "POST"
          "headers"
          ==> createObj
                  [ "Content-Type" ==> "application/json"
                    "Authorization" ==> $"Bearer {apiKey}"
                    "Connection" ==> "keep-alive" ]
          "body" ==> body
          "redirect" ==> "manual"
          if signal.IsSome then
              "signal" ==> signal.Value ]

let responseMethod0 (response: obj) (methodName: string) : obj = response?(methodName) ()

/// Effective gateway base URL: `OLLAMA_API_BASE` env override (E2E mock server) or production Ollama default.
let webApiBase () : string =
    let env = nodeProcess?env

    let envValue =
        if Dyn.isNullish env then
            ""
        else
            let v = env?("OLLAMA_API_BASE")
            if Dyn.isNullish v then "" else string v

    Wanxiangshu.Kernel.Config.webApiBase envValue

let getWebApiKey () : string =
    let env = nodeProcess?env

    if Dyn.isNullish env then
        ""
    else
        let key = env?("OLLAMA_API_KEY")
        if Dyn.isNullish key then "" else string key

let getOllamaApiKey () : string = getWebApiKey ()

let requireWebApiKey (apiKey: string) : Result<string, string> =
    let trimmed = apiKey.Trim()

    if trimmed = "" then
        Error "Missing OLLAMA_API_KEY environment variable."
    else
        Ok trimmed

let private validatedWebApiKey () : Result<string, DomainError> =
    requireWebApiKey (getWebApiKey ())
    |> Result.mapError (fun _ -> UpstreamRefused "Missing OLLAMA_API_KEY environment variable.")

let private normalizeWebApiPath (pathname: string) : string =
    if pathname.StartsWith("/") then
        pathname
    else
        $"/{pathname}"

/// POST JSON to the agent backend gateway (OLLAMA_API_KEY bearer), returning parsed JSON.
let webApiPost (pathname: string) (body: obj) (abortSignal: obj option) : JS.Promise<Result<obj, DomainError>> =
    promise {
        match validatedWebApiKey () with
        | Error e -> return Error e
        | Ok apiKey ->
            let url = $"{webApiBase ()}{normalizeWebApiPath pathname}"
            let bodyStr = JS.JSON.stringify body
            let init = postInit apiKey bodyStr abortSignal

            let rec fetchStep (currentUrl: string) (depth: int) : JS.Promise<Result<obj, DomainError>> =
                promise {
                    if depth > 5 then
                        return Error(UpstreamRefused "Too many redirects")
                    else
                        try
                            let! (response: obj) = fetch currentUrl init
                            let status = unbox<float> (Dyn.get response "status")
                            if status >= 300.0 && status < 400.0 then
                                let loc = getResponseLocation response
                                if System.String.IsNullOrEmpty loc then
                                    return Error(UpstreamRefused $"Ollama API error ({status}): redirect missing Location header")
                                else
                                    let nextUrl = resolveUrl loc currentUrl
                                    match validateFetchUrl nextUrl with
                                    | Error msg -> 
                                        return Error(UpstreamRefused $"redirect to private IP blocked: {msg}")
                                    | Ok() ->
                                        return! fetchStep nextUrl (depth + 1)
                            else
                                let ok = Dyn.truthy (Dyn.get response "ok")
                                if not ok then
                                    let! text =
                                        promise {
                                            try
                                                let! (t: string) = response?text ()
                                                return t
                                            with _ ->
                                                return ""
                                        }
                                    let statusText = Dyn.str response "statusText"
                                    let detail = if text <> "" then text else statusText
                                    return Error(UpstreamRefused $"Ollama API error ({status}): {detail}")
                                else
                                    let! (json: obj) = response?json ()
                                    return Ok json
                        with ex ->
                            return Error(UnknownJsError ex.Message)
                }
            return! fetchStep url 0
    }

let ollamaPost (pathname: string) (body: obj) (abortSignal: obj option) : JS.Promise<Result<obj, DomainError>> =
    let path =
        if pathname.StartsWith("/") then
            pathname
        else
            $"/{pathname}"

    webApiPost path body abortSignal

/// `web_fetch` body must pass SSRF validation before POST.
let ollamaPostWebFetch (body: obj) (abortSignal: obj option) : JS.Promise<Result<obj, DomainError>> =
    promise {
        let url = Dyn.str body "url"

        match validateFetchUrl url with
        | Error msg -> return Error(UpstreamRefused msg)
        | Ok() -> return! ollamaPost "/web_fetch" body abortSignal
    }
