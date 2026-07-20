module Wanxiangshu.Runtime.Wanxiangzhen.SessionIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

[<Emit("Promise.resolve($0)")>]
let private resolveThenable (x: obj) : JS.Promise<obj> = jsNative

[<Emit("typeof $0")>]
let private jsTypeOf (x: obj) : string = jsNative

let getSession (client: obj) : Result<obj, string> =
    let session = get client "session"

    if isNullish session then
        Error "client.session missing"
    else
        Ok session

/// Structured failure diagnostic for host/console (testable pure builder).
let promptFailureDiagnostic
    (reason: string)
    (sessionId: string)
    (detail: string)
    : obj =
    createObj
        [ "event", box "wanxiangzhen_prompt_session_failed"
          "reason", box reason
          "sessionId", box sessionId
          "detail", box detail ]

let private logPromptFailure (reason: string) (sessionId: string) (detail: string) : unit =
    JS.console.error (promptFailureDiagnostic reason sessionId detail)

let buildPromptArg (sessionId: string) (text: string) : obj =
    let part = createObj [ "type", box "text"; "text", box text ]

    createObj
        [ "path", box (createObj [ "id", box sessionId ])
          "body", box (createObj [ "parts", box [| part |] ]) ]

let promptSession (client: obj) (sessionId: string) (text: string) : JS.Promise<unit> =
    promise {
        if isNullish client then
            let detail = "wanxiangzhen_prompt_parameter_missing: client cannot be null"
            logPromptFailure "parameter_missing" "" detail
            return raise (System.ArgumentNullException("client", detail))
        elif System.String.IsNullOrWhiteSpace(sessionId) then
            let detail = "wanxiangzhen_prompt_parameter_missing: sessionId cannot be null or empty"
            logPromptFailure "parameter_missing" sessionId detail
            return raise (System.ArgumentException(detail, "sessionId"))
        elif System.String.IsNullOrWhiteSpace(text) then
            let detail = "wanxiangzhen_prompt_parameter_missing: text cannot be null or empty"
            logPromptFailure "parameter_missing" sessionId detail
            return raise (System.ArgumentException(detail, "text"))
        else
            match getSession client with
            | Error err ->
                let detail = "wanxiangzhen_session_api_missing:" + err
                logPromptFailure "session_api_missing" sessionId detail
                return raise (System.Exception(detail))
            | Ok session ->
                let promptFn = get session "prompt"

                if isNullish promptFn || jsTypeOf promptFn <> "function" then
                    let detail = "wanxiangzhen_session_api_missing: session.prompt function missing"
                    logPromptFailure "session_api_missing" sessionId detail
                    return raise (System.Exception(detail))
                else
                    try
                        let arg = buildPromptArg sessionId text
                        let res = session?("prompt") (arg)
                        let! _ = resolveThenable res
                        return ()
                    with ex ->
                        logPromptFailure "invocation_failed" sessionId ex.Message
                        return raise ex
    }

let clientId (hookInput: obj) : string = str hookInput "sessionID"
