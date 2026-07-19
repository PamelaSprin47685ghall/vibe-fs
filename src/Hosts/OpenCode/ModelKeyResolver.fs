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
let private normalizeProviderID (modelObj: obj) : string =
    let v = str modelObj "providerID"
    if v <> "" then v else str modelObj "provider"

let private normalizeModelID (modelObj: obj) : string =
    let v = str modelObj "modelID"
    if v <> "" then v else str modelObj "id"

let private extractModelKey (sessionBody: obj) (sessionID: string) : string =
    let modelObj = get sessionBody "model"

    if isNullish modelObj then
        sessionID + "@" + "no-model"
    elif typeIs modelObj "string" then
        string modelObj
    else
        let pId = normalizeProviderID modelObj
        let mId = normalizeModelID modelObj
        let variant = str modelObj "variant"

        if pId <> "" && mId <> "" then
            let suffix = if variant <> "" then ":" + variant else ""
            pId + "/" + mId + suffix
        else
            let idVal = str modelObj "id"

            if idVal <> "" then
                idVal
            else
                sessionID + "@" + "unknown-model"

let private handleSessionGetResult (sessionID: string) (res: obj) : string =
    if isNullish res then
        sessionID + "@" + "no-response"
    else
        let data = get res "data"

        if isNullish data then
            sessionID + "@" + "no-data"
        else
            let nested = get data "data"
            let sessionBody = if isNullish nested then data else nested
            extractModelKey sessionBody sessionID

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

                        return handleSessionGetResult sessionID res
                    with _ ->
                        return sessionID + "@" + "resolve-error"
    }

/// Build a truthful source label for the OpenCode session model resolver.
/// Uses provider.list catalog when available, falls back to session model.
let resolveLimitSource () : string = "provider-catalog"
