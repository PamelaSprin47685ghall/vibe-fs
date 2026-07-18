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
            // N-01 / Wanxiangzhen fix: host API missing must be a typed
            // failure, not a silent "ok".  Re-raise so the caller
            // (CoordinatorReplay) can log and skip; previously the
            // function returned () on missing API and the caller
            // happily assumed the warning was delivered.
            return raise (System.Exception("wanxiangzhen_session_api_missing:" + err))
        | Ok session ->
            let part = createObj [ "type", box "text"; "text", box text ]

            let arg =
                createObj
                    [ "path", box (createObj [ "id", box sessionId ])
                      "body", box (createObj [ "parts", box [| part |] ]) ]

            let! _ = session?("prompt") (arg) |> unbox<JS.Promise<obj>>
            return ()
    }

let clientId (hookInput: obj) : string = str hookInput "sessionID"
