module Wanxiangshu.Shell.ContextBudgetUsageCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn

let isBacklogEncodedMessage (host: Host) (msg: obj) : bool =
    if isNullish msg then
        false
    else
        let idVal = get msg "id"

        let isPrefix =
            not (isNullish idVal)
            && typeIs idVal "string"
            && (string idVal).StartsWith(backlogPrefixIdPrefix)

        let parts = get msg "parts"

        let isProjection =
            if not (isNullish parts) && typeIs parts "object" && isArray parts then
                let arr = unbox<obj array> parts

                arr
                |> Array.exists (fun p ->
                    let toolVal =
                        let t = get p "tool"

                        if not (isNullish t) then
                            t
                        else
                            let tn = get p "toolName"
                            if not (isNullish tn) then tn else box ""

                    let toolStr = string toolVal
                    let state = get p "state"

                    if toolStr = todoWriteToolName host && not (isNullish state) then
                        let outputVal = get state "output"

                        not (isNullish outputVal)
                        && typeIs outputVal "string"
                        && (string outputVal).Contains("Completed work from folded turns")
                    else
                        false)
            else
                false

        isPrefix || isProjection

let backlogBytesFromEncoded (host: Host) (encodedAll: obj array) : int =
    let mutable sum = 0

    for msg in encodedAll do
        if isBacklogEncodedMessage host msg then
            sum <- sum + JS.JSON.stringify(msg).Length

    sum

type private LimitResolution =
    | InputLimit of int
    | ContextLimit of int

let private valueOf =
    function
    | InputLimit v -> v
    | ContextLimit v -> v

let private extractLimitFromModelDetailed (modelObj: obj) : LimitResolution option =
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

let private extractLimitFromModel (modelObj: obj) : int option =
    extractLimitFromModelDetailed modelObj |> Option.map valueOf

let private tryExtractMaxInputTokensDetailed (target: obj) : LimitResolution option =
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

let private tryGetSessionModelRef (target: obj) (sessionID: string) : JS.Promise<(string * string) option> =
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

let private tryGetModelLimitFromProviderListDetailed
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

let private tryGetModelLimitFromProviderList
    (target: obj)
    (modelId: string)
    (providerId: string)
    : JS.Promise<int option> =
    promise {
        let! res = tryGetModelLimitFromProviderListDetailed target modelId providerId
        return res |> Option.map valueOf
    }

let private tryGetMaxInputTokensAsyncDetailed (target: obj) (sessionID: string) : JS.Promise<LimitResolution option> =
    promise {
        let! modelRefOpt = tryGetSessionModelRef target sessionID

        match modelRefOpt with
        | None -> return None
        | Some(modelId, providerId) ->
            let! limit = tryGetModelLimitFromProviderListDetailed target modelId providerId
            return limit
    }

let tryGetMaxInputTokensAsync (target: obj) (sessionID: string) : JS.Promise<int option> =
    promise {
        let! res = tryGetMaxInputTokensAsyncDetailed target sessionID
        return res |> Option.map valueOf
    }

let tryGetRealContextUsage (target: obj) (sessionID: string) : (obj array -> JS.Promise<int option>) option =
    if isNullish target then
        None
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
            None
        else
            let sessionApi = get client "session"

            if isNullish sessionApi || isNullish (get sessionApi "get") then
                None
            else
                Some(fun (_encoded: obj array) ->
                    promise {
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
                                    let tokens = get data "tokens"

                                    if isNullish tokens then
                                        return None
                                    else
                                        let inputVal = get tokens "input"
                                        let cacheObj = get tokens "cache"

                                        let cacheRead =
                                            if isNullish cacheObj then
                                                0.0
                                            else
                                                let r = get cacheObj "read"

                                                if isNullish r || not (typeIs r "number") then
                                                    0.0
                                                else
                                                    unbox<float> r

                                        if isNullish inputVal || not (typeIs inputVal "number") then
                                            return None
                                        else
                                            let inputNum = int (unbox<float> inputVal)
                                            let cacheNum = int cacheRead
                                            return Some(inputNum + cacheNum)
                        with _ ->
                            return None
                    })

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
