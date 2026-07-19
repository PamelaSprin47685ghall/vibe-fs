module Wanxiangshu.Runtime.Wanxiangzhen.SessionIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

let getSession (client: obj) : Result<obj, string> =
    let session = get client "session"

    if isNullish session then
        Error "client.session missing"
    else
        Ok session

let promptSession (client: obj) (sessionId: string) (text: string) : JS.Promise<unit> =
    promise {
        match getSession client with
        | Error err ->
            let exMsg = "wanxiangzhen_session_api_missing:" + err
            JS.console.error ("SessionIo.promptSession failed: " + exMsg)
            return raise (System.Exception(exMsg))
        | Ok session ->
            let promptFn = get session "prompt"

            if isNullish promptFn then
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

                    let! _ = session?("prompt") (arg) |> unbox<JS.Promise<obj>>
                    return ()
                with ex ->
                    JS.console.error ("SessionIo.promptSession invocation failed: " + ex.Message)
                    return raise ex
    }

let clientId (hookInput: obj) : string = str hookInput "sessionID"
