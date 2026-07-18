module Wanxiangshu.Hosts.Opencode.OpenCodeModelResolution

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.FileSys

let private logDebug (_fmt: string) (_args: obj[]) : unit = ()

/// Strongly-typed model resolution result.
type ModelResolutionResult =
    { ProviderID: string
      ModelID: string
      UsableInputTokens: int
      Source: string }

let private fallbackMaxInputTokens = 8192
let private defaultOutputReserve = 32000

/// Call provider.list({query:{directory}}) to get the full model catalog.
let private tryProviderList (client: obj) (directory: string) : JS.Promise<obj option> =
    promise {
        let providerApi = get client "provider"

        let listFn =
            if isNullish providerApi then
                null
            else
                get providerApi "list"

        if isNullish listFn || not (typeIs listFn "function") then
            return None
        else
            let listArg = createObj [ "query", box (createObj [ "directory", box directory ]) ]

            try
                let! res = unbox<JS.Promise<obj>> (providerApi?list (listArg))
                return Some res
            with _ ->
                return None
    }

/// Extract {context, output} limit from a model definition in the provider catalog.
let extractLimitFromCatalogEntry (modelDef: obj) : (int * int) option =
    if isNullish modelDef then
        None
    else
        let limitObj = get modelDef "limit"

        if isNullish limitObj then
            None
        else
            let ctxVal = get limitObj "context"
            let outVal = get limitObj "output"
            let inputVal = get limitObj "input"

            // Prefer explicit input if available (newer SDK), otherwise use context
            let contextTokens =
                if not (isNullish inputVal) && typeIs inputVal "number" then
                    int (unbox<float> inputVal)
                elif not (isNullish ctxVal) && typeIs ctxVal "number" then
                    int (unbox<float> ctxVal)
                else
                    0

            let outputTokens =
                if not (isNullish outVal) && typeIs outVal "number" then
                    int (unbox<float> outVal)
                else
                    0

            if contextTokens > 0 then
                Some(contextTokens, outputTokens)
            else
                None

/// Find a model definition in the provider catalog by providerID and modelID.
let findModelInCatalog (catalogData: obj) (providerID: string) (modelID: string) : obj option =
    if isNullish catalogData then
        None
    else
        let data =
            if isArray catalogData then
                catalogData :?> obj array
            else
                let inner = get catalogData "data"

                if not (isNullish inner) && isArray inner then
                    inner :?> obj array
                else
                    [||]

        data
        |> Array.tryPick (fun entry ->
            let pId = str entry "providerID"
            let mId = str entry "id"

            if pId = providerID && mId = modelID then
                Some entry
            else
                None)

/// Compute usable input tokens from context and output limits.
/// Formula: max(0, context - min(nonzero output, 32000))
let computeUsableInputTokens (contextTokens: int) (outputTokens: int) : int =
    let outputReserve =
        if outputTokens > 0 then
            min outputTokens defaultOutputReserve
        else
            defaultOutputReserve

    max 0 (contextTokens - outputReserve)

/// Resolve model key and usable input tokens from the provider catalog.
let resolveModelResolution
    (client: obj)
    (providerID: string)
    (modelID: string)
    (directory: string)
    : JS.Promise<ModelResolutionResult> =
    promise {
        if isNullish client || providerID = "" || modelID = "" then
            return
                { ProviderID = providerID
                  ModelID = modelID
                  UsableInputTokens = fallbackMaxInputTokens
                  Source = "fallback-8192" }
        else
            let! catalogOpt = tryProviderList client directory

            match catalogOpt with
            | None ->
                return
                    { ProviderID = providerID
                      ModelID = modelID
                      UsableInputTokens = fallbackMaxInputTokens
                      Source = "fallback-8192" }
            | Some catalogRes ->
                let catalogData = get catalogRes "data"

                match findModelInCatalog catalogData providerID modelID with
                | None ->
                    return
                        { ProviderID = providerID
                          ModelID = modelID
                          UsableInputTokens = fallbackMaxInputTokens
                          Source = "fallback-8192" }
                | Some modelDef ->
                    match extractLimitFromCatalogEntry modelDef with
                    | None ->
                        return
                            { ProviderID = providerID
                              ModelID = modelID
                              UsableInputTokens = fallbackMaxInputTokens
                              Source = "fallback-8192" }
                    | Some(contextTokens, outputTokens) ->
                        let usable = computeUsableInputTokens contextTokens outputTokens

                        let source =
                            if outputTokens > 0 then
                                "provider-catalog-input-reserved"
                            else
                                "provider-catalog-context"

                        return
                            { ProviderID = providerID
                              ModelID = modelID
                              UsableInputTokens = usable
                              Source = source }
    }

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
