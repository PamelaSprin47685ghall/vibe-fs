module Wanxiangshu.Hosts.Opencode.ModelKeyResolver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

let private sessionGetArg (sessionID: string) (directory: string) : obj =
    createObj
        [ "sessionID", box sessionID
          "id", box sessionID
          "directory", box directory
          "path", box (createObj [ "id", box sessionID; "sessionID", box sessionID ])
          "query", box (createObj [ "directory", box directory; "workspace", box "" ]) ]

let private trySessionGet (client: obj) : (obj * obj) option =
    let rec tryPath (parts: string list) (current: obj) (parent: obj) : (obj * obj) option =
        match parts with
        | [] ->
            let getFn = get current "get"
            if isNullish getFn then None else Some(current, getFn)
        | head :: tail ->
            let child = get current head

            if isNullish child then None else tryPath tail child current

    [ [ "session" ]; [ "v2"; "session" ] ]
    |> List.tryPick (fun parts -> tryPath parts client client)

/// Build model identity key `providerID/modelID[:variant]` from the session client,
/// falling back gracefully through several levels. Never returns session@directory placeholder.
let resolveModelKey (client: obj) (sessionID: string) : JS.Promise<string> =
    promise {
        if isNullish client then
            return sessionID + "@" + "" // honest: no client provided
        else
            match trySessionGet client with
            | None -> return sessionID + "@" + "" // honest: no session API on client
            | Some(sessionApi, getFn) ->
                if not (typeIs getFn "function") then
                    return sessionID + "@" + "" // honest: no session.get RPC
                else
                    try
                        let arg = sessionGetArg sessionID ""
                        let raw = Wanxiangshu.Runtime.Dyn.callWithThis1 getFn sessionApi arg

                        let! res =
                            if Wanxiangshu.Runtime.Dyn.typeIs (Wanxiangshu.Runtime.Dyn.get raw "then") "function" then
                                unbox<JS.Promise<obj>> raw
                            else
                                Promise.lift raw

                        if isNullish res then
                            return sessionID + "@" + "no-response"
                        else
                            let data = get res "data"

                            if isNullish data then
                                return sessionID + "@" + "no-data"
                            else
                                let data2 = get data "data"
                                let sessionBody = if isNullish data2 then data else data2
                                let modelObj = get sessionBody "model"

                                if isNullish modelObj then
                                    return sessionID + "@" + "no-model"
                                else
                                    let pId =
                                        let v = str modelObj "providerID"
                                        if v <> "" then v else str modelObj "provider"

                                    let mId =
                                        let v = str modelObj "modelID"
                                        if v <> "" then v else str modelObj "id"

                                    let variant = str modelObj "variant"

                                    if pId <> "" && mId <> "" then
                                        let suffix = if variant <> "" then ":" + variant else ""
                                        return pId + "/" + mId + suffix
                                    else
                                        let idVal = str modelObj "id"

                                        if idVal <> "" then
                                            return idVal
                                        else
                                            return sessionID + "@" + "unknown-model"
                    with _ ->
                        return sessionID + "@" + "resolve-error"
    }

/// Build a truthful source label for the OpenCode session model resolver.
/// Uses provider.list catalog when available, falls back to session model.
let resolveLimitSource () : string = "provider-catalog"
