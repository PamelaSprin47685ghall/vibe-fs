module Wanxiangshu.Hosts.Opencode.OpenCodeModelResolutionProviders

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.FileSys

/// Strongly-typed model resolution result.
type ModelResolutionResult =
    { ProviderID: string
      ModelID: string
      UsableInputTokens: int
      Source: string }

let internal fallbackMaxInputTokens = 8192
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
