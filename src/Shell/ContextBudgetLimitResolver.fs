module Wanxiangshu.Shell.ContextBudgetLimitResolver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn

type LimitResolution =
    | InputLimit of int
    | ContextLimit of int

let valueOf =
    function
    | InputLimit v -> v
    | ContextLimit v -> v

let extractLimitFromModelDetailed (modelObj: obj) : LimitResolution option =
    if isNullish modelObj then
        None
    else
        let limitObj = get modelObj "limit"

        if isNullish limitObj then
            None
        else
            let inputVal = get limitObj "input"

            if not (isNullish inputVal) && typeIs inputVal "number" then
                Some(InputLimit(int (unbox<float> inputVal)))
            else
                let ctxVal = get limitObj "context"

                if not (isNullish ctxVal) && typeIs ctxVal "number" then
                    Some(ContextLimit(int (unbox<float> ctxVal)))
                else
                    None

let extractLimitFromModel (modelObj: obj) : int option =
    extractLimitFromModelDetailed modelObj |> Option.map valueOf

let tryExtractMaxInputTokensDetailed (target: obj) : LimitResolution option =
    if isNullish target then
        None
    else
        let trySessionModel (sess: obj) =
            if isNullish sess then
                None
            else
                extractLimitFromModelDetailed (get sess "model")

        match trySessionModel target with
        | Some limit -> Some limit
        | None ->
            let sessionObj = get target "session"

            match trySessionModel sessionObj with
            | Some limit -> Some limit
            | None ->
                let clientObj = get target "client"

                let clientSession =
                    if not (isNullish clientObj) then
                        get clientObj "session"
                    else
                        box null

                trySessionModel clientSession

let tryExtractMaxInputTokens (target: obj) : int option =
    tryExtractMaxInputTokensDetailed target |> Option.map valueOf

let tryGetSessionModelRef (target: obj) (sessionID: string) : JS.Promise<(string * string) option> =
    promise {
        if isNullish target then
            return None
        else
            let client =
                if not (isNullish (get target "session")) then
                    target
                else
                    let c = get target "client"

                    if not (isNullish c) && not (isNullish (get c "session")) then
                        c
                    else
                        box null

            if isNullish client then
                return None
            else
                let sessionApi = get client "session"

                let getFn =
                    if isNullish sessionApi then
                        box null
                    else
                        get sessionApi "get"

                if isNullish getFn || not (typeIs getFn "function") then
                    return None
                else
                    try
                        let arg = createObj [ "sessionID", box sessionID ]
                        let! res = unbox<JS.Promise<obj>> (sessionApi?get (arg))

                        if isNullish res then
                            return None
                        else
                            let data = get res "data"

                            if isNullish data then
                                return None
                            else
                                let modelObj = get data "model"

                                if isNullish modelObj then
                                    return None
                                else
                                    let mId = string (get modelObj "id")
                                    let pId = string (get modelObj "providerID")

                                    if mId = "" || pId = "" then
                                        return None
                                    else
                                        return Some(mId, pId)
                    with _ ->
                        return None
    }

let tryGetModelLimitFromProviderListDetailed
    (target: obj)
    (modelId: string)
    (providerId: string)
    : JS.Promise<LimitResolution option> =
    promise {
        if isNullish target then
            return None
        else
            let providerApi = get target "provider"

            if isNullish providerApi then
                return None
            else
                let listFn = get providerApi "list"

                if isNullish listFn || not (typeIs listFn "function") then
                    return None
                else
                    try
                        let! res = unbox<JS.Promise<obj>> (providerApi?list (box null))

                        if isNullish res then
                            return None
                        else
                            let data = get res "data"

                            if isNullish data then
                                return None
                            else
                                let allArr = get data "all"

                                if isNullish allArr || not (isArray allArr) then
                                    return None
                                else
                                    let providers = unbox<obj array> allArr

                                    let provider =
                                        providers |> Array.tryFind (fun p -> string (get p "id") = providerId)

                                    match provider with
                                    | None -> return None
                                    | Some prov ->
                                        let modelsObj = get prov "models"

                                        if isNullish modelsObj then
                                            return None
                                        else
                                            return extractLimitFromModelDetailed (get modelsObj modelId)
                    with _ ->
                        return None
    }

let tryGetModelLimitFromProviderList (target: obj) (modelId: string) (providerId: string) : JS.Promise<int option> =
    promise {
        let! res = tryGetModelLimitFromProviderListDetailed target modelId providerId
        return res |> Option.map valueOf
    }

let tryGetMaxInputTokensAsyncDetailed (target: obj) (sessionID: string) : JS.Promise<LimitResolution option> =
    promise {
        if isNullish target then
            return None
        else
            let client =
                if not (isNullish (get target "session")) then
                    target
                else
                    let c = get target "client"

                    if not (isNullish c) && not (isNullish (get c "session")) then
                        c
                    else
                        box null

            if isNullish client then
                return None
            else
                let sessionApi = get client "session"

                if isNullish sessionApi || isNullish (get sessionApi "get") then
                    return None
                else
                    try
                        let arg = createObj [ "sessionID", box sessionID ]
                        let! res = unbox<JS.Promise<obj>> (sessionApi?get (arg))

                        if isNullish res then
                            return None
                        else
                            let data = get res "data"

                            if isNullish data then
                                return None
                            else
                                let modelObj = get data "model"

                                match extractLimitFromModelDetailed modelObj with
                                | Some limit -> return Some limit
                                | None ->
                                    if isNullish modelObj then
                                        return None
                                    else
                                        let mId = string (get modelObj "id")
                                        let pId = string (get modelObj "providerID")

                                        if mId = "" || pId = "" then
                                            return None
                                        else
                                            let! limit = tryGetModelLimitFromProviderListDetailed target mId pId

                                            return limit
                    with _ ->
                        return None
    }

let tryGetMaxInputTokensAsync (target: obj) (sessionID: string) : JS.Promise<int option> =
    promise {
        let! res = tryGetMaxInputTokensAsyncDetailed target sessionID
        return res |> Option.map valueOf
    }

let resolveMaxInputTokens (targets: obj list) (sessionID: string) : JS.Promise<int> =
    promise {
        let syncRes = targets |> List.map tryExtractMaxInputTokensDetailed |> List.choose id

        let syncInputOpt =
            syncRes
            |> List.tryPick (function
                | InputLimit v -> Some v
                | _ -> None)

        match syncInputOpt with
        | Some limit -> return limit
        | None ->
            let syncContextOpt =
                syncRes
                |> List.tryPick (function
                    | ContextLimit v -> Some v
                    | _ -> None)

            let mutable asyncInputOpt = None
            let mutable asyncContextOpt = None

            for t in targets do
                if asyncInputOpt.IsNone then
                    let! limitRes = tryGetMaxInputTokensAsyncDetailed t sessionID

                    match limitRes with
                    | Some(InputLimit v) -> asyncInputOpt <- Some v
                    | Some(ContextLimit v) when asyncContextOpt.IsNone -> asyncContextOpt <- Some v
                    | _ -> ()

            match asyncInputOpt with
            | Some limit -> return limit
            | None ->
                match asyncContextOpt with
                | Some limit -> return limit
                | None -> return Option.defaultValue 0 syncContextOpt
    }
