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

let promptSession (client: obj) (sessionId: string) (text: string) : JS.Promise<unit> =
    promise {
        if isNullish client then
            return raise (System.ArgumentNullException("client", "wanxiangzhen_prompt_parameter_missing: client cannot be null"))
        elif System.String.IsNullOrWhiteSpace(sessionId) then
            return raise (System.ArgumentException("wanxiangzhen_prompt_parameter_missing: sessionId cannot be null or empty", "sessionId"))
        elif System.String.IsNullOrWhiteSpace(text) then
            return raise (System.ArgumentException("wanxiangzhen_prompt_parameter_missing: text cannot be null or empty", "text"))
        else
            match getSession client with
            | Error err ->
                let exMsg = "wanxiangzhen_session_api_missing:" + err
                JS.console.error ("SessionIo.promptSession failed: " + exMsg)
                return raise (System.Exception(exMsg))
            | Ok session ->
                let promptFn = get session "prompt"

                if isNullish promptFn || jsTypeOf promptFn <> "function" then
                    let exMsg = "wanxiangzhen_session_api_missing: session.prompt function missing"
                    JS.console.error ("SessionIo.promptSession failed: " + exMsg)
                    return raise (System.Exception(exMsg))
                else
                    try
                        let part = createObj [ "type", box "text"; "text", box text ]

                        let arg =
                            createObj
                                [ "path", box (createObj [ "id", box sessionId ])
                                  "body", box (createObj [ "parts", box [| part |] ]) ]

                        let res = session?("prompt") (arg)
                        let! _ = resolveThenable res
                        return ()
                    with ex ->
                        JS.console.error ("SessionIo.promptSession invocation failed: " + ex.Message)
                        return raise ex
    }

let clientId (hookInput: obj) : string = str hookInput "sessionID"
