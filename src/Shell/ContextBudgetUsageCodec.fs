module Wanxiangshu.Shell.ContextBudgetUsageCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn

let isBacklogEncodedMessage (host: Host) (msg: obj) : bool =
    if isNullish msg then false
    else
        let idVal = get msg "id"
        let isPrefix = not (isNullish idVal) && typeIs idVal "string" && (string idVal).StartsWith(backlogPrefixIdPrefix)
        let parts = get msg "parts"
        let isProjection =
            if not (isNullish parts) && typeIs parts "object" && isArray parts then
                let arr = unbox<obj array> parts
                arr |> Array.exists (fun p ->
                    let toolVal =
                        let t = get p "tool"
                        if not (isNullish t) then t
                        else
                            let tn = get p "toolName"
                            if not (isNullish tn) then tn else box ""
                    let toolStr = string toolVal
                    let state = get p "state"
                    if toolStr = todoWriteToolName host && not (isNullish state) then
                        let outputVal = get state "output"
                        not (isNullish outputVal) && typeIs outputVal "string" && (string outputVal).Contains("Completed work from folded turns")
                    else false)
            else false
        isPrefix || isProjection

let backlogBytesFromEncoded (host: Host) (encodedAll: obj array) : int =
    let mutable sum = 0
    for msg in encodedAll do
        if isBacklogEncodedMessage host msg then
            sum <- sum + JS.JSON.stringify(msg).Length
    sum

let tryGetGetContextUsage (target: obj) : (obj array -> JS.Promise<int option>) option =
    if isNullish target then None
    else
        let prop = get target "getContextUsage"
        if isNullish prop || not (typeIs prop "function") then None
        else
            Some (fun (encoded: obj array) ->
                promise {
                    try
                        let res = target?getContextUsage(encoded)
                        if isNullish res then
                            return None
                        else
                            let thenProp = get res "then"
                            if not (isNullish thenProp) && typeIs thenProp "function" then
                                let! resolved = unbox<JS.Promise<obj>> res
                                if isNullish resolved then return None
                                elif typeIs resolved "number" then return Some (int (unbox<float> resolved))
                                elif typeIs resolved "object" then
                                    let props = [ "tokenCount"; "inputTokens"; "totalTokens"; "tokens"; "input_tokens"; "total_tokens"; "usage" ]
                                    return props |> List.tryPick (fun p ->
                                        let v = get resolved p
                                        if not (isNullish v) && typeIs v "number" then Some (int (unbox<float> v)) else None)
                                else return None
                            elif typeIs res "number" then
                                return Some (int (unbox<float> res))
                            elif typeIs res "object" then
                                let props = [ "tokenCount"; "inputTokens"; "totalTokens"; "tokens"; "input_tokens"; "total_tokens"; "usage" ]
                                return props |> List.tryPick (fun p ->
                                    let v = get res p
                                    if not (isNullish v) && typeIs v "number" then Some (int (unbox<float> v)) else None)
                            else
                                return None
                    with _ ->
                        return None
                })

let tryGetRealContextUsage (target: obj) (sessionID: string) : (obj array -> JS.Promise<int option>) option =
    if isNullish target then None
    else
        let client =
            if not (isNullish (get target "session")) then target
            else
                let c = get target "client"
                if not (isNullish c) && not (isNullish (get c "session")) then c
                else box null

        if isNullish client then None
        else
            let sessionApi = get client "session"
            if isNullish sessionApi || isNullish (get sessionApi "get") then None
            else
                Some (fun (_encoded: obj array) ->
                    promise {
                        try
                            let arg = createObj [ "path", createObj [ "id", box sessionID ] ]
                            let! res = unbox<JS.Promise<obj>> (sessionApi?get(arg))
                            
                            let getOptProp (name: string) (o: obj) : obj option =
                                if isNullish o then None
                                else
                                    let v = get o name
                                    if isNullish v then None else Some v

                            let tokenCount =
                                Some res
                                |> Option.bind (getOptProp "data")
                                |> Option.bind (getOptProp "tokens")
                                |> Option.bind (fun tokens ->
                                    match getOptProp "total" tokens with
                                    | Some t when typeIs t "number" -> Some (int (unbox<float> t))
                                    | _ ->
                                        match getOptProp "input" tokens with
                                        | Some i when typeIs i "number" -> Some (int (unbox<float> i))
                                        | _ -> None)
                            
                            return tokenCount
                        with _ ->
                            return None
                    })

let tryExtractMaxInputTokens (target: obj) : int option =
    if isNullish target then None
    else
        let extract (o: obj) : int option =
            if isNullish o then None
            else
                let v1 = get o "maxInputTokens"
                if not (isNullish v1) && typeIs v1 "number" then
                    Some (int (unbox<float> v1))
                else
                    let v2 = get o "contextWindow"
                    if not (isNullish v2) && typeIs v2 "number" then
                        Some (int (unbox<float> v2))
                    else None
        match extract target with
        | Some limit -> Some limit
        | None ->
            let sessionObj = get target "session"
            match extract sessionObj with
            | Some limit -> Some limit
            | None ->
                let clientObj = get target "client"
                let clientSession =
                    if not (isNullish clientObj) then
                        get clientObj "session"
                    else box null
                match extract clientSession with
                | Some limit -> Some limit
                | None ->
                    let modelObj =
                        if not (isNullish sessionObj) then
                            get sessionObj "model"
                        else box null
                    extract modelObj

let tryGetMaxInputTokensAsync (target: obj) (sessionID: string) :
    JS.Promise<int option> =
    promise {
        if isNullish target then return None
        else
            let sessionOrNull s =
                not (isNullish s) && not (isNullish (get s "session"))
            let client =
                if not (isNullish (get target "session")) then
                    target
                else
                    let c = get target "client"
                    if sessionOrNull c then c
                    else box null
            if isNullish client then return None
            else
                let sessionApi = get client "session"
                let getFn =
                    if isNullish sessionApi then box null
                    else get sessionApi "get"
                let hasGet =
                    not (isNullish getFn) && typeIs getFn "function"
                if isNullish sessionApi || not hasGet then
                    return None
                else
                    try
                        let arg =
                            createObj [
                                "path",
                                createObj [ "id", box sessionID ]
                            ]
                        let! res =
                            unbox<JS.Promise<obj>> (sessionApi?get(arg))
                        if isNullish res then return None
                        else
                            let getOpt name o =
                                if isNullish o then None
                                else
                                    let v = get o name
                                    if isNullish v then None
                                    else Some v
                            let extract o =
                                if isNullish o then None
                                else
                                    let v1 = get o "maxInputTokens"
                                    if not (isNullish v1)
                                       && typeIs v1 "number" then
                                        Some (int (unbox<float> v1))
                                    else
                                        let v2 =
                                            get o "contextWindow"
                                        if
                                            not (isNullish v2)
                                            && typeIs v2 "number" then
                                            Some (int (unbox<float> v2))
                                        else None
                            let dataOpt = getOpt "data" res
                            match dataOpt with
                            | Some data ->
                                match extract data with
                                | Some limit -> return Some limit
                                | None ->
                                    let modelOpt = getOpt "model" data
                                    match modelOpt with
                                    | Some model -> return extract model
                                    | None -> return None
                            | None -> return extract res
                    with _ -> return None
    }

let resolveMaxInputTokens (targets: obj list) (sessionID: string) :
    JS.Promise<int> =
    promise {
        let syncOpt = targets |> List.tryPick tryExtractMaxInputTokens
        match syncOpt with
        | Some limit when limit > 0 -> return limit
        | _ ->
            let mutable result = None
            for t in targets do
                if result.IsNone then
                    let! limit =
                        tryGetMaxInputTokensAsync t sessionID
                    if limit.IsSome && limit.Value > 0 then
                        result <- limit
            match result with
            | Some limit -> return limit
            | None -> return 200000
    }
