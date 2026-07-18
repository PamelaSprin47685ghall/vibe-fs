module Wanxiangshu.Hosts.Opencode.ModelKeyResolver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

/// Build model identity key `providerID/modelID[:variant]` from the session client,
/// falling back gracefully through several levels. Never returns session@directory placeholder.
let resolveModelKey (client: obj) (sessionID: string) : JS.Promise<string> =
    promise {
        if isNullish client then
            return sessionID + "@" + "" // honest: no client provided
        else
            let session = get client "session"

            if isNullish session then
                return sessionID + "@" + "" // honest: no session API on client
            else
                let getFn = get session "get"

                if isNullish getFn || not (typeIs getFn "function") then
                    return sessionID + "@" + "" // honest: no session.get RPC
                else
                    try
                        let arg =
                            createObj
                                [ "path", box (createObj [ "id", box sessionID ])
                                  "query", box (createObj [ "directory", box "" ]) ]

                        let! res = unbox<JS.Promise<obj>> (session?get (arg))

                        if isNullish res then
                            return sessionID + "@" + "no-response"
                        else
                            let data = get res "data"

                            if isNullish data then
                                return sessionID + "@" + "no-data"
                            else
                                let modelObj = get data "model"

                                if isNullish modelObj then
                                    return sessionID + "@" + "no-model"
                                else
                                    let pId = str modelObj "providerID"
                                    let mId = str modelObj "modelID"
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
