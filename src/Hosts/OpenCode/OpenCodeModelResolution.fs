module Wanxiangshu.Hosts.Opencode.OpenCodeModelResolution

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Opencode.ModelResolutionHelpers

module ProviderHelpers = Wanxiangshu.Hosts.Opencode.OpenCodeModelResolutionProviders
let private fallbackMaxInputTokens = ProviderHelpers.fallbackMaxInputTokens

/// Strongly-typed model resolution result.
type ModelResolutionResult = ProviderHelpers.ModelResolutionResult

let extractLimitFromCatalogEntry =
    ModelResolutionHelpers.extractLimitFromCatalogEntry

let findModelInCatalog = ModelResolutionHelpers.findModelInCatalog
let computeUsableInputTokens = ModelResolutionHelpers.computeUsableInputTokens
let resolveModelResolution = ProviderHelpers.resolveModelResolution

let private sessionGetArg (sessionID: string) (directory: string) : obj =
    createObj
        [ "sessionID", box sessionID
          "id", box sessionID
          "directory", box directory
          "path", box (createObj [ "id", box sessionID; "sessionID", box sessionID ])
          "query", box (createObj [ "directory", box directory; "workspace", box "" ]) ]

/// Locate a session API on either the root client or client.v2.
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

let private extractProviderAndModel (modelObj: obj) : string * string =
    if isNullish modelObj then
        ("", "")
    elif typeIs modelObj "string" then
        let modelStr = string modelObj
        let parts = modelStr.Split([| '/' |])

        if parts.Length = 2 then
            (parts.[0], parts.[1])
        else
            ("", modelStr)
    else
        let pId =
            let v = str modelObj "providerID"
            if v <> "" then v else str modelObj "provider"

        let mId =
            let v = str modelObj "modelID"
            if v <> "" then v else str modelObj "id"

        if pId <> "" && mId <> "" then
            (pId, mId)
        else
            let idVal = str modelObj "id"
            if idVal <> "" then ("", idVal) else ("", "")

/// Extract {providerID, modelID} from the session's last user message model ref.
let private extractModelFromSession
    (client: obj)
    (sessionID: string)
    (directory: string)
    : JS.Promise<string * string> =
    promise {
        let sessionApiOpt = trySessionGet client

        match sessionApiOpt with
        | None -> return ("", "")
        | Some(sessionApi, getFn) ->
            if not (typeIs getFn "function") then
                return ("", "")
            else
                let arg = sessionGetArg sessionID directory

                try
                    let raw = Wanxiangshu.Runtime.Dyn.callWithThis1 getFn sessionApi arg

                    let! sessionRes =
                        if Wanxiangshu.Runtime.Dyn.typeIs (Wanxiangshu.Runtime.Dyn.get raw "then") "function" then
                            unbox<JS.Promise<obj>> raw
                        else
                            Promise.lift raw

                    if isNullish sessionRes then
                        return ("", "")
                    else
                        let data = get sessionRes "data"

                        if isNullish data then
                            return ("", "")
                        else
                            let data2 = get data "data"
                            let sessionBody = if isNullish data2 then data else data2
                            return extractProviderAndModel (get sessionBody "model")
                with _ ->
                    return ("", "")
    }

let resolveMaxInputTokens (sessionID: string) (client: obj) (directory: string) : JS.Promise<int> =
    promise {
        if isNullish client then
            return fallbackMaxInputTokens
        else
            let! (providerID, modelID) = extractModelFromSession client sessionID directory

            if providerID = "" && modelID = "" then
                let! fallback =
                    Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens [ client ] sessionID directory

                return fallback
            else
                let! result = resolveModelResolution client providerID modelID directory

                let limitTarget =
                    createObj
                        [ "model",
                          box (createObj [ "limit", box (createObj [ "input", box result.UsableInputTokens ]) ]) ]

                return!
                    Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens
                        [ limitTarget; client ]
                        sessionID
                        directory
    }

/// Resolve the full model resolution result including source information.
let resolveModelResolutionResult
    (sessionID: string)
    (client: obj)
    (directory: string)
    : JS.Promise<ModelResolutionResult> =
    promise {
        if isNullish client then
            return
                { ProviderID = ""
                  ModelID = ""
                  UsableInputTokens = fallbackMaxInputTokens
                  Source = "fallback-8192" }
        else
            let! (providerID, modelID) = extractModelFromSession client sessionID directory

            if providerID = "" && modelID = "" then
                return
                    { ProviderID = ""
                      ModelID = ""
                      UsableInputTokens = fallbackMaxInputTokens
                      Source = "fallback-8192" }
            else
                return! resolveModelResolution client providerID modelID directory
    }
