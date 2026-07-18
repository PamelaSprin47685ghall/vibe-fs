module Wanxiangshu.Hosts.Opencode.OpenCodeModelResolution

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.FileSys

module ProviderHelpers = Wanxiangshu.Hosts.Opencode.OpenCodeModelResolutionProviders

let private logDebug (_fmt: string) (_args: obj[]) : unit = ()
let private fallbackMaxInputTokens = ProviderHelpers.fallbackMaxInputTokens

/// Strongly-typed model resolution result.
type ModelResolutionResult = ProviderHelpers.ModelResolutionResult

let extractLimitFromCatalogEntry = ProviderHelpers.extractLimitFromCatalogEntry
let findModelInCatalog = ProviderHelpers.findModelInCatalog
let computeUsableInputTokens = ProviderHelpers.computeUsableInputTokens
let resolveModelResolution = ProviderHelpers.resolveModelResolution

/// Extract {providerID, modelID} from the session's last user message model ref.
let private extractModelFromSession
    (client: obj)
    (sessionID: string)
    (directory: string)
    : JS.Promise<string * string> =
    promise {
        let sessionApi = get client "session"
        let getFn = if isNullish sessionApi then null else get sessionApi "get"

        if isNullish getFn || not (typeIs getFn "function") then
            return ("", "")
        else
            let sessionArg =
                createObj
                    [ "path", box (createObj [ "id", box sessionID ])
                      "query", box (createObj [ "directory", box directory ]) ]

            try
                let! sessionRes = unbox<JS.Promise<obj>> (sessionApi?get (sessionArg))

                if isNullish sessionRes then
                    return ("", "")
                else
                    let data = get sessionRes "data"

                    if isNullish data then
                        return ("", "")
                    else
                        let modelObj = get data "model"

                        if isNullish modelObj then
                            return ("", "")
                        else
                            let pId = str modelObj "providerID"
                            let mId = str modelObj "modelID"

                            if pId <> "" && mId <> "" then
                                return (pId, mId)
                            else
                                let idVal = str modelObj "id"
                                return (if idVal <> "" then ("", idVal) else ("", ""))
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
                return! Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens [ client ] sessionID directory
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
