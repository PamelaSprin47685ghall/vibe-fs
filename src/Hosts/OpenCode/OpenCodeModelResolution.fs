module Wanxiangshu.Hosts.Opencode.OpenCodeModelResolution

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.FileSys

let private logDebug (_fmt: string) (_args: obj[]) : unit = ()

let private tryConfigGet (client: obj) (directory: string) : JS.Promise<obj option> =
    promise {
        let configApi = get client "config"
        let configGet = if isNullish configApi then null else get configApi "get"

        if isNullish configGet || not (typeIs configGet "function") then
            return None
        else
            let configArg =
                createObj [ "query", box (createObj [ "directory", box directory ]) ]

            let! configRes = unbox<JS.Promise<obj>> (configApi?get (configArg))
            return Some configRes
    }

let private trySessionGet (client: obj) (sessionID: string) (directory: string) : JS.Promise<obj option> =
    promise {
        let sessionApi = get client "session"
        let sessionGet = if isNullish sessionApi then null else get sessionApi "get"

        if isNullish sessionGet || not (typeIs sessionGet "function") then
            return None
        else
            let sessionArg =
                createObj
                    [ "path", box (createObj [ "id", box sessionID ])
                      "query", box (createObj [ "directory", box directory ]) ]

            let! sessionRes = unbox<JS.Promise<obj>> (sessionApi?get (sessionArg))
            return Some sessionRes
    }

let private resolveLimitFromModelObj (configData: obj) (modelObj: obj) : int option =
    let providerID = str modelObj "providerID"
    let modelID = str modelObj "id"

    let providers = get configData "provider"

    let providerObj =
        if isNullish providers then
            null
        else
            get providers providerID

    let models = get providerObj "models"
    let modelDef = if isNullish models then null else get models modelID

    let limitObj = get modelDef "limit"

    if isNullish limitObj then
        None
    else
        let inputVal = get limitObj "input"
        let ctxVal = get limitObj "context"
        let raw = if isNullish inputVal then ctxVal else inputVal

        if isNullish raw || not (typeIs raw "number") then
            None
        else
            Some(int (unbox<float> raw))

let private tryModelLimitFromClientConfig
    (client: obj)
    (sessionID: string)
    (directory: string)
    : JS.Promise<int option> =
    promise {
        if isNullish client then
            return None
        else
            let! configResOpt = tryConfigGet client directory

            match configResOpt with
            | None -> return None
            | Some configRes ->
                let configData = get configRes "data"
                let! sessionResOpt = trySessionGet client sessionID directory

                match sessionResOpt with
                | None -> return None
                | Some sessionRes ->
                    let sessionData = get sessionRes "data"
                    let modelObj = get sessionData "model"
                    return resolveLimitFromModelObj configData modelObj
    }

let resolveMaxInputTokens (sessionID: string) (client: obj) (directory: string) : JS.Promise<int> =
    promise {
        let! limitOpt = tryModelLimitFromClientConfig client sessionID directory

        match limitOpt with
        | Some limit ->
            let limitTarget =
                createObj [ "model", box (createObj [ "limit", box (createObj [ "input", box limit ]) ]) ]

            return!
                Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens
                    [ limitTarget; client ]
                    sessionID
                    directory
        | None ->
            return! Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens [ client ] sessionID directory
    }
