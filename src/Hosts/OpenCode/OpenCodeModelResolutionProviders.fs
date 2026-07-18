module Wanxiangshu.Hosts.Opencode.OpenCodeModelResolutionProviders

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Opencode.ModelResolutionHelpers

/// Strongly-typed model resolution result.
type ModelResolutionResult =
    { ProviderID: string
      ModelID: string
      UsableInputTokens: int
      Source: string }

let internal fallbackMaxInputTokens = 8192

/// Try v2 model.list({location:{directory}}), then model.list, then provider.list.
let private tryListApi (apiName: string) (client: obj) (directory: string) : JS.Promise<obj option> =
    promise {
        let parts = apiName.Split([| '.' |])

        let rec navigate (obj: obj) (parts: string list) (parent: obj) : (obj * obj) option =
            match parts with
            | [] -> Some(parent, obj)
            | head :: tail ->
                let child = get obj head

                if isNullish child then None else navigate child tail obj

        match navigate client (Array.toList parts) client with
        | None -> return None
        | Some(parent, listFn) ->
            if not (typeIs listFn "function") then
                return None
            else
                let listArg =
                    if apiName.Contains "v2" then
                        createObj [ "location", box (createObj [ "directory", box directory ]) ]
                    else
                        createObj [ "query", box (createObj [ "directory", box directory ]) ]

                try
                    let raw = Wanxiangshu.Runtime.Dyn.callWithThis1 listFn parent listArg

                    let! res =
                        if Wanxiangshu.Runtime.Dyn.typeIs (Wanxiangshu.Runtime.Dyn.get raw "then") "function" then
                            unbox<JS.Promise<obj>> raw
                        else
                            Promise.lift raw

                    return Some res
                with _ ->
                    return None
    }

let private tryModelList (client: obj) (directory: string) : JS.Promise<obj option> =
    promise {
        let! v2 = tryListApi "v2.model.list" client directory

        match v2 with
        | Some _ -> return v2
        | None ->
            let! legacy = tryListApi "model.list" client directory

            match legacy with
            | Some _ -> return legacy
            | None ->
                let! provider = tryListApi "provider.list" client directory
                return provider
    }

/// Resolve model key and usable input tokens from the provider catalog.
let resolveModelResolution
    (client: obj)
    (providerID: string)
    (modelID: string)
    (directory: string)
    : JS.Promise<ModelResolutionResult> =
    promise {
        let fallback =
            { ProviderID = providerID
              ModelID = modelID
              UsableInputTokens = fallbackMaxInputTokens
              Source = "fallback-8192" }

        if isNullish client || providerID = "" || modelID = "" then
            return fallback
        else
            let! catalogOpt = tryModelList client directory

            match catalogOpt with
            | None -> return fallback
            | Some catalogRes ->
                let catalogData = get catalogRes "data"

                match findModelInCatalog catalogData providerID modelID with
                | None -> return fallback
                | Some modelDef ->
                    match extractLimitFromCatalogEntry modelDef with
                    | None -> return fallback
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
